using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core.Composing;
using Umbraco.Core.Events;
using Umbraco.Core.Models;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Implement;
using UmbracoFlare.Configuration;
using UmbracoFlare.ImageCropperHelpers;

namespace UmbracoFlare.Components
{
    public class DataTypeSavingComponent : IComponent
    {
        private readonly ICloudflareConfiguration _cloudflareConfiguration;
        private readonly IImageCropperManager imageCropperManager;

        public DataTypeSavingComponent(ICloudflareConfiguration cloudflareConfiguration,IImageCropperManager imageCropperManager)
        {
            _cloudflareConfiguration = cloudflareConfiguration;
            this.imageCropperManager = imageCropperManager;
        }

        public void Initialize()
        {
            if (_cloudflareConfiguration.PurgeCacheOn)
            {
                DataTypeService.Saved += RefreshImageCropsCache; 
            }
        }

        public void Terminate()
        {
            DataTypeService.Saved -= RefreshImageCropsCache;
        }

        private void RefreshImageCropsCache(IDataTypeService sender, SaveEventArgs<IDataType> e)
        {
            //A data type has saved, see if it was a 
            IEnumerable<IDataType> imageCroppers = imageCropperManager.GetImageCropperDataTypes(true);
            
            if (imageCroppers.Intersect(e.SavedEntities).Any())
            {
                //There were some freshly saved Image cropper data types so refresh the image crop cache.
                //We can do that by simply getting the crops
                imageCropperManager.GetAllCrops(true); //true to bypass the cache & refresh it.
            }
        }
    }
}
