using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Composing;
using Umbraco.Web.Models.Trees;
using Umbraco.Web.Trees;
using UmbracoFlare.Configuration;

namespace UmbracoFlare.Components
{
   
    public class TreeMenuComponent : IComponent
    {
        private readonly bool _showPurgeMenu;

        public TreeMenuComponent(ICloudflareConfiguration cloudflareConfiguration)
        {
            _showPurgeMenu = cloudflareConfiguration.ShowPurgeMenu;
        }
        
        public void Initialize()
        {
            TreeControllerBase.MenuRendering += AddPurgeCacheForContentMenu;
        }

        public void Terminate()
        {
            TreeControllerBase.MenuRendering -= AddPurgeCacheForContentMenu;
        }
        private void AddPurgeCacheForContentMenu(TreeControllerBase sender, MenuRenderingEventArgs e)
        {
            //if we are not in content menu or we should not show the tree in content menu as set in cloudflare config
            //then do not show purge cache option in tree menu for content
            if (sender.TreeAlias != "content" || !_showPurgeMenu)
            {
                return;
            }

            MenuItem menuItem = new MenuItem("purgeCache", "Purge Cloudflare Cache");

            menuItem.Icon = "umbracoflare-tiny";
            menuItem.OpensDialog = true;
            menuItem.LaunchDialogView("/App_Plugins/UmbracoFlare/backoffice/treeViews/PurgeCacheDialog.html", "Purge Cloudflare Cache");

            e.Menu.Items.Insert(e.Menu.Items.Count - 1, menuItem);
        }
    }
}
