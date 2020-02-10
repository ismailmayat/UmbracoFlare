﻿using UmbracoFlare.Configuration;
using UmbracoFlare.Manager;
using UmbracoFlare.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using Umbraco.Web.Mvc;
using Umbraco.Web.WebApi;
using System.IO;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;
using UmbracoFlare.Helpers;
using Umbraco.Core.Models.PublishedContent;
using UmbracoFlare.FileSystemPickerControllers;

namespace UmbracoFlare.ApiControllers
{
    [PluginController("UmbracoFlare")]
    public class CloudflareUmbracoApiController : UmbracoAuthorizedApiController
    {
        //The Log
        private readonly ICloudflareManager _cloudflareManager;
   
        private readonly IUrlWildCardManager _wildCardManager;
        private readonly ILogger _logger;

        public CloudflareUmbracoApiController(ICloudflareManager cloudflareManager, IUrlWildCardManager wildCardManager,ILogger logger)
        {
            _cloudflareManager = cloudflareManager;
           
            _wildCardManager = wildCardManager;
            
            _logger = logger;
        }

        [HttpPost]
        public StatusWithMessage PurgeCacheForUrls([FromBody]PurgeCacheForUrlsRequestModel model)
        {
            /*Important to note that the urls can come in here in two different ways. 
             *1) They can come in here without domains on them. If that is the case then the domains property should have values.
             *      1a) They will need to have the urls built by appending each domain to each url. These urls technically might not exist
             *          but that is the responsibility of whoever called this method to ensure that. They will still go to cloudflare even know the
             *          urls physically do not exists, which is fine because it won't cause an error. 
             *2) They can come in here with domains, if that is the case then we are good to go, no work needed.
             * 
             * */
            
            if (model.Urls == null || !model.Urls.Any()) 
            { 
                return new StatusWithMessage( false, "You must provide urls to clear the cache for.") ;
            }

            List<string> builtUrls = new List<string>();

            //Check to see if there are any domains. If there are, then we know that we need to build the urls using the domains
            if(model.Domains != null && model.Domains.Any())
            {
                builtUrls.AddRange(UrlHelper.MakeFullUrlWithDomain(model.Urls, model.Domains, true));   
            }
            else
            {
                builtUrls = model.Urls.ToList();
            }

            builtUrls.AddRange(AccountForWildCards(builtUrls));
            
            IEnumerable<StatusWithMessage> results = _cloudflareManager.PurgePages(builtUrls);

            if (results.Count() == 0)
            {
                return new StatusWithMessage(false, "error purging please check log");
            }

            if(results.Any(x => !x.Success))
            {
                string errorMessage = _cloudflareManager.PrintResultsSummary(results);
                
                this.Logger.Error(typeof(CloudflareUmbracoApiController),errorMessage + " attempted to process " + string.Join(",", builtUrls));
                
                return new StatusWithMessage(false, errorMessage);
            }

            return new StatusWithMessage(true, String.Format("{0} urls purged successfully.", results.Count(x => x.Success)));
        }

        [HttpPost]
        public StatusWithMessage PurgeStaticFiles([FromBody]PurgeStaticFilesRequestModel model)
        {
            List<string> allowedFileExtensions = new List<string>(){".css", ".js", ".jpg", ".png", ".gif", ".aspx", ".html"};   
            string generalSuccessMessage = "Successfully purged the cache for the selected static files.";
            string generalErrorMessage = "Sorry, we could not purge the cache for the static files.";
            if (model.StaticFiles == null)
            {
                return new StatusWithMessage(false, generalErrorMessage);
            }

            if (!model.StaticFiles.Any())
            {
                return new StatusWithMessage(true, generalSuccessMessage);
            }

            List<StatusWithMessage> errors;
            IEnumerable<string> allFilePaths = GetAllFilePaths(model.StaticFiles, out errors);

            //Save a list of each individual file if it errors so we can give detailed errors to the user.
            List<StatusWithMessage> results = new List<StatusWithMessage>();

            List<string> fullUrlsToPurge = new List<string>();
            //build the urls with the domain we are on now
            foreach (string filePath in allFilePaths)
            {
                string extension = Path.GetExtension(filePath);

                if(!allowedFileExtensions.Contains(extension))
                {
                    //results.Add(new StatusWithMessage(false, String.Format("You cannot purge the file {0} because its extension is not allowed.", filePath)));
                }
                else
                {
                    fullUrlsToPurge.AddRange(UrlHelper.MakeFullUrlWithDomain(filePath, model.Hosts, true));                    
                }
            }

             results.AddRange(_cloudflareManager.PurgePages(fullUrlsToPurge));

            if (results.Any(x => !x.Success))
            {
                return new StatusWithMessage(false, _cloudflareManager.PrintResultsSummary(results));
            }
            else
            {
                return new StatusWithMessage(true, String.Format("{0} static files purged successfully.", results.Where(x => x.Success).Count()));
            }
        }



        private IEnumerable<string> GetAllFilePaths(string[] filesOrFolders, out List<StatusWithMessage> errors)
        {
            errors = new List<StatusWithMessage>();

            string rootOfApplication = IOHelper.MapPath("~/");

            List<string> filePaths = new List<string>();

            //the static files could have files or folders, we are sure at this point so we need to collect all of the 
            //files.
            FileSystem fileSystemApi = new FileSystem();

            foreach (string fileOrFolder in filesOrFolders)
            {
                if (String.IsNullOrEmpty(fileOrFolder)) continue;

                try
                {
                    //Map the file path to the server.
                    string fileOrFolderPath = IOHelper.MapPath(fileOrFolder);
                    FileAttributes attr = System.IO.File.GetAttributes(fileOrFolderPath);

                    //Check to see if its a folder
                    if ((attr & System.IO.FileAttributes.Directory) == FileAttributes.Directory)
                    {
                        IEnumerable<FileInfo> filesInTheFolder = fileSystemApi.GetFilesIncludingSubDirs(fileOrFolderPath);

                        filePaths.AddRange(filesInTheFolder.Select(x =>
                        {
                            string directory = x.Directory.FullName.Replace(rootOfApplication, "");
                            directory = directory.Replace("\\", "/");
                            return directory + "/" + x.Name;
                        }));
                    }
                    else
                    {   
                        

                        if (!System.IO.File.Exists(fileOrFolderPath))
                        {
                            //File does not exist, continue and log the error.
                            errors.Add(new StatusWithMessage(false, String.Format("Could not find file with the path {0}", fileOrFolderPath)));
                            continue;
                        }

                        if (fileOrFolder.StartsWith("/"))
                        {
                            filePaths.Add(fileOrFolder.TrimStart('/'));
                        }
                        else
                        {
                            filePaths.Add(fileOrFolder);
                        }
                        
                    }
                }
                catch(Exception e)
                {
                    _logger.Error<CloudflareUmbracoApiController>(e);
                }
                
            }

            return filePaths;
        }


        [HttpPost]
        public StatusWithMessage PurgeAll()
        {
            //it doesn't matter what domain we pick bc it will purge everything. 
            IEnumerable<string> domains = _cloudflareManager.DomainManager.GetDomainsFromCloudflareZones();

            List<StatusWithMessage> results = new List<StatusWithMessage>();

            foreach(string domain in domains)
            {
                results.Add(_cloudflareManager.PurgeEverything(domain));
            }

            return new StatusWithMessage() { Success = !results.Any(x => !x.Success), Message = _cloudflareManager.PrintResultsSummary(results) };
        }

        /// <summary>
        /// this is called by purge cache dialog from content tree
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        [HttpPost]
        public StatusWithMessage PurgeCacheForContentNode([FromBody] PurgeCacheForIdParams args)
        {
            if (args.nodeId <= 0) { return new StatusWithMessage(false, "You must provide a node id."); }

            if (!_cloudflareManager.Configuration.PurgeCacheOn) { return new StatusWithMessage(false, CloudflareMessages.CLOULDFLARE_DISABLED); }
            
            IPublishedContent content = Umbraco.Content(args.nodeId);
            
            var urls = BuildUrlsToPurge(content, args.purgeChildren);

            StatusWithMessage resultFromPurge = PurgeCacheForUrls(new PurgeCacheForUrlsRequestModel() { Urls = urls, Domains = null });
            if(resultFromPurge.Success)
            {
                return new StatusWithMessage(true, String.Format("{0}", resultFromPurge.Message));
            }

            return resultFromPurge;
        }

        [HttpGet]
        public IEnumerable<string> GetAllowedDomains()
        {
            return _cloudflareManager.DomainManager.AllowedDomains;
        }


        private List<string> BuildUrlsToPurge(IPublishedContent contentToPurge, bool includeChildren)
        {
            List<string> urls = new List<string>();
            
            if(contentToPurge == null)
            {
                return urls;
            }

            urls.AddRange(_cloudflareManager.DomainManager.GetUrlsForNode(contentToPurge.Id, includeChildren));
            
            return urls;
        }


        private IEnumerable<string> AccountForWildCards(IEnumerable<string> urls)
        {
            IEnumerable<string> urlsWithWildCards = urls.Where(x => x.Contains('*'));

            if(urlsWithWildCards == null || !urlsWithWildCards.Any())
            {
                return urls;
            }


            return this._wildCardManager.GetAllUrlsForWildCardUrls(urlsWithWildCards);
        }
    }

    public class PurgeCacheForIdParams
    {
        public int nodeId { get; set; }
        public bool purgeChildren { get; set; }
    }
}
