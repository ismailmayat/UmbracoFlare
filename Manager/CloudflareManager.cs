using UmbracoFlare.ApiControllers;
using UmbracoFlare.Configuration;
using UmbracoFlare.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UmbracoFlare.Helpers;
using UmbracoFlare.Services;
using Umbraco.Core.Logging;

namespace UmbracoFlare.Manager
{
    public class CloudflareManager : ICloudflareManager
    {
        private ICloudflareConfiguration configuration;
        private readonly IUmbracoFlareDomainManager domainManager;
        private ICloudflareService cloudflareService;
        private readonly IProfilingLogger logger;
        private IEnumerable<Zone> _zonesCache = null;

        public ICloudflareConfiguration Configuration => configuration;
        public IUmbracoFlareDomainManager DomainManager => domainManager;

        public CloudflareManager(
                ICloudflareConfiguration configuration, 
                IUmbracoFlareDomainManager domainManager,
                ICloudflareService cloudflareProvider,
                IProfilingLogger logger
            )
        {
            this.configuration = configuration;
            this.domainManager = domainManager;
            this.cloudflareService = cloudflareProvider;
            this.logger = logger;
        }


        public StatusWithMessage PurgeEverything(string domain)
        {
            //If the setting is turned off, then don't do anything.
            if (!configuration.PurgeCacheOn) return new StatusWithMessage() { Success = false, Message = "Clould flare for umbraco is turned of as indicated in the config file." };


            //We only want the host and not the scheme or port number so just to ensure that is what we are getting we will
            //proccess it as a uri.
            Uri domainAsUri;
            try
            {
                domainAsUri= new Uri(domain);
                domain = domainAsUri.Authority;
            }
            catch(Exception e)
            {
                logger.Error<CloudflareManager>(e);
                //So if we are here it didn't parse as an uri so we will assume that it was given in the correct format (without http://)
            }

            //Get the zone for the given domain
            Zone websiteZone = GetZone(domain);

            if (websiteZone == null)
            {
                //this will already be logged in the GetZone method so just relay that it was bad.
                return new StatusWithMessage(false, String.Format("We could not purge the cache because the domain {0} is not valid with the provided api key and email combo. Please ensure this domain is registered under these credentials on your cloudflare dashboard.", domain));
            }

            bool statusFromApi = this.cloudflareService.PurgeCache(websiteZone.Id, null, true);

            if(!statusFromApi)
            {
                return new StatusWithMessage(false, CloudflareMessages.CLOUDFLARE_API_ERROR);
            }
            else
            {
                return new StatusWithMessage(true, ""); 
            }
        }
        

        public IEnumerable<StatusWithMessage> PurgePages(IEnumerable<string> urls)
        {
            //If the setting is turned off, then don't do anything.
            if (!configuration.PurgeCacheOn) return new List<StatusWithMessage>(){new StatusWithMessage(false, CloudflareMessages.CLOULDFLARE_DISABLED)};

            urls = domainManager.FilterToAllowedDomains(urls);

            //Separate all of these into individual groups where the domain is the same that way we save some cloudflare requests.
            IEnumerable<IGrouping<string, string>> groupings = urls.GroupBy(url => UrlHelper.GetDomainFromUrl(url,true));

            List<StatusWithMessage> results = new List<StatusWithMessage>();

            //Now loop through each group.
            foreach (IGrouping<string, string> domainUrlGroup in groupings)
            {

                //get the domain without the scheme or port.
                Uri domain = new UriBuilder(domainUrlGroup.Key).Uri;

                Zone websiteZone = GetZone(domain.DnsSafeHost);

                if (websiteZone == null)
                {
                    string errorMessage = $"Could not retrieve the zone from cloudflare with the domain(url) of {domain}";
                    
                    logger.Error<CloudflareManager>(errorMessage);
                    //this will already be logged in the GetZone method so just relay that it was bad.
                    results.Add(new StatusWithMessage(false, errorMessage));
                    continue; //to the next domain group.
                }

                //Make the request to the api using the urls from this domain group.
                bool apiResult = this.cloudflareService.PurgeCache(websiteZone.Id, domainUrlGroup);

                if (!apiResult)
                {
                    logger.Error<CloudflareManager>(CloudflareMessages.CLOUDFLARE_API_ERROR);
                    results.Add(new StatusWithMessage(false, CloudflareMessages.CLOUDFLARE_API_ERROR));
                }
                else
                {
                    foreach (string url in domainUrlGroup)
                    {
                        logger.Debug<CloudflareManager>($"Purged for url {url}");
                        //We need to  add x number of statuswithmessages that are true where x is the number urls
                        results.Add(new StatusWithMessage(true, String.Format("Purged for url {0}", url)));
                    }
                }
            }
            //return the results of all of the api calls.
            return results;
        }


        /// <summary>
        /// This will get a zone by domain(url)
        /// </summary>
        /// <param name="url">The url of the domain that we are getting the domain for.</param>
        /// <returns>The retreived zone</returns>
        public Zone GetZone(string url = null)
        {
            IEnumerable<Zone> zones = domainManager.AllowedZones.Where(x => url.Contains(x.Name));
            
            if(zones == null || !zones.Any())
            {
                logger.Error<CloudflareManager>(String.Format("Could not retrieve the zone from cloudflare with the domain(url) of {0}", url));
                return null;
            }

            var zone = zones.First();

            logger.Debug<CloudflareManager>($"found {zone.Name} for url {url}");
            
            return zone;
        }


      
        public IEnumerable<Zone> ListZones()
        {
            if(_zonesCache == null || !_zonesCache.Any())
            {
                _zonesCache = this.cloudflareService.ListZones();
            }
            return _zonesCache;
        }

        
        public string PrintResultsSummary(IEnumerable<StatusWithMessage> results)
        {
            StringBuilder sb = new StringBuilder();
            
            //Show the number of successes
            sb.AppendLine(String.Format("There were {0} successes.", results.Count(x => x.Success)));

            foreach(StatusWithMessage failedStatus in results.Where(x=> !x.Success))
            {
                sb.AppendLine("Failed for reason: " + failedStatus.Message + ".  ");
            }

            return sb.ToString();
        }

        

    }
}
