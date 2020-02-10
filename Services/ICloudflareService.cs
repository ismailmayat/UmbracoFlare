using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UmbracoFlare.Models;

namespace UmbracoFlare.Services
{
    public interface ICloudflareService
    {
        IEnumerable<Zone> ListZones(string domainName = null, bool throwExceptionOnFail = false);
        bool PurgeCache(string zoneIdentifier, IEnumerable<string> urls, bool purgeEverything = false, bool throwExceptionOnError = false);
    }
}
