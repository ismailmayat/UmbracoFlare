using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using UmbracoFlare.Configuration;
using UmbracoFlare.Manager;

namespace UmbracoFlare.Components
{
    public class ContentPublishedComponent : IComponent
    {
        private readonly ICloudflareConfiguration _cloudflareConfiguration;
        private readonly ICloudflareManager _cloudflareManager;
        private readonly IUmbracoFlareDomainManager _domainManager;
        private readonly IUrlWildCardManager _wildCardManager;
        private readonly ILogger _logger;


        public ContentPublishedComponent(ICloudflareConfiguration cloudflareConfiguration,
            ICloudflareManager cloudflareManager, 
            IUrlWildCardManager wildCardManager,ILogger logger)
        {
            _cloudflareConfiguration = cloudflareConfiguration;
            _cloudflareManager = cloudflareManager;
            _domainManager = cloudflareManager.DomainManager;
            _wildCardManager = wildCardManager;
            _logger = logger;

        }
        public void Initialize()
        {
            if (_cloudflareConfiguration.PurgeCacheOn)
            {
                ContentService.Published += Content_Published;
            }
        }

        private void Content_Published(IContentService sender, ContentPublishedEventArgs e)
        {
            foreach (IContent content in e.PublishedEntities)
            {
                PurgeCloudflareCache(content);
                UpdateContentIdToUrlCache(content);
            }
        }

        public void Terminate()
        {
      
        }

        private void PurgeCloudflareCache(IContent content)
        {

            var urls = new List<string>();
            
            try
            {
                //Check to see if the page has cache purging on publish disabled.
                if (content.HasProperty("cloudflareDisabledOnPublish") && content.GetValue<bool>("cloudflareDisabledOnPublish"))
                {
                    //it was disabled so just continue;
                    return;
                }
            }
            catch (Exception ex)
            {
                //ignore
                _logger.Error<ContentPublishedComponent>(ex);
            }

            urls.AddRange(_domainManager.GetUrlsForNode(content.Id, false));
        
            var results = _cloudflareManager.PurgePages(urls);

            if (results.Any() && results.Where(x => !x.Success).Any())
            {
                string errorMessage = "We could not purge the Cloudflare cache. \n \n" + _cloudflareManager.PrintResultsSummary(results);
                
                _logger.Error<ContentPublishedComponent>(errorMessage + " " + string.Join(",",urls));
            }
            else
            {
                _logger.Debug<ContentPublishedComponent>($"purged urls {string.Join(",",urls)}");
            }
        }

        private void UpdateContentIdToUrlCache(IContent content)
        {
            if (content.Published)
            {
                IEnumerable<string> urls = _domainManager.GetUrlsForNode(content.Id, false);

                if (urls.Contains("#"))
                {
                    //When a piece of content is first saved, we cannot get the url, if that is the case then we need to just
                    //invalidate the who ContentIdToUrlCache, that way when we request all of the urls agian, it will pick it up.
                    _wildCardManager.DeletedContentIdToUrlCache();
                }
                else
                {
                    _wildCardManager.UpdateContentIdToUrlCache(content.Id, urls);
                }
            }
        }
    }
}
