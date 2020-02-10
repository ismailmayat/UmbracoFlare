using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UmbracoFlare.Configuration
{
    public interface ICloudflareConfiguration
    {
        bool PurgeCacheOn { get; set; }
        
        bool ShowPurgeMenu { get; set; }
        
        string Token { get; set; }
        string ValidDomain { get; set; }
    }
}
