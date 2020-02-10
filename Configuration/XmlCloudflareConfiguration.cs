using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Hosting;
using System.Xml.Linq;

namespace UmbracoFlare.Configuration
{
    public class XmlCloudflareConfiguration : ICloudflareConfiguration
    {
        public static string CONFIG_PATH;
        private XDocument _doc = null;

        public XmlCloudflareConfiguration()
        {
            try
            {
                CONFIG_PATH = HostingEnvironment.MapPath("~/Config/cloudflare.config");
                this._doc = XDocument.Load(CONFIG_PATH);
            }
            catch(Exception e)
            {

            }
        }
        
        public bool ShowPurgeMenu 
        {
            get
            {
                bool showPurgeMenu = false;
                if(this._doc!=null)
                {
                    bool.TryParse(this._doc.Root.Element("showPurgeMenu").Value, out showPurgeMenu);
                }
                return showPurgeMenu;
            }

            set
            {
                this._doc.Root.Element("showPurgeMenu").SetValue(value.ToString());
                this._doc.Save(CONFIG_PATH);
            }
        }
        
        public bool PurgeCacheOn 
        {
            get
            {
                bool purgeCacheOn = false;
                if(this._doc!=null)
                {
                    bool.TryParse(this._doc.Root.Element("purgeCacheOn").Value, out purgeCacheOn);
                }
                return purgeCacheOn;
            }

            set
            {
                this._doc.Root.Element("purgeCacheOn").SetValue(value.ToString());
                this._doc.Save(CONFIG_PATH);
            }
        }

        public string Token
        {
            get
            {
                return this._doc==null ? String.Empty: this._doc.Root.Element("token").Value;
            }
            set
            {
                this._doc.Root.Element("token").SetValue(value);
                this._doc.Save(CONFIG_PATH);
            }
        }

        public string ValidDomain
        {
            get
            {
                return this._doc==null ? String.Empty: this._doc.Root.Element("validDomain").Value;
            }
            set
            {
                this._doc.Root.Element("validDomain").SetValue(value);
                this._doc.Save(CONFIG_PATH);
            }
        }
    }
}
