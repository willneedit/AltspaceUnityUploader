#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace AltSpace_Unity_Uploader
{
    public abstract class AltspaceListItem
    {
        public struct Parameters
        {
            public bool isExclusivelyAndroid;
            public string uploadFileName;
        }
        protected class UploadContentMethods
        {
            public static HttpContent BundleContent(AltspaceListItem item, Parameters parm)
            {
                var zipContents = new ByteArrayContent(File.ReadAllBytes(parm.uploadFileName));
                zipContents.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                var colorSpace = PlayerSettings.colorSpace == ColorSpace.Linear ? "linear" : "gamma";
                var srp = PlayerSettings.stereoRenderingPath == StereoRenderingPath.Instancing
                    ? (parm.isExclusivelyAndroid ? "spmv" : "spi")
                    : (PlayerSettings.stereoRenderingPath == StereoRenderingPath.SinglePass)
                        ? "sp"
                        : "mp";

                return new MultipartFormDataContent
                {
                    { new StringContent("" + Common.usingUnityVersion), item.type + "[game_engine_version]" },
                    { new StringContent(srp), item.type + "[stereo_render_mode]" },
                    { new StringContent(colorSpace), item.type + "[color_space]" },
                    { zipContents, item.type + "[zip]", item.bundleName + ".zip" }
                };

            }

            public static HttpContent GLTFContent(AltspaceListItem item)
            {
                var zipContents = new ByteArrayContent(File.ReadAllBytes(item.itemPath));
                zipContents.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                return new MultipartFormDataContent
                {
                    { zipContents, item.type + "[gltf]", item.bundleName + ".glb" }
                };
            }
        }
        private string _itemPath = null;

        public string itemName = null;      // Name of the online Altspace item, raw version. Null if no selection
        public string id = null;            // ID. Null if no selection

        public List<assetBundleJSON> asset_bundles = null;  // (Unity Assets) Asset Bundles connected to the Altspace Item
        public string item_url = null;                      // (Flat File Assets) URL to download the asset from

        public string imageFile = null;
        public string description = null;
        public string tag_list = null;

        /// <summary>
        /// get and set itemPath. Triggers a save into the association list when needed
        /// </summary>
        public string itemPath 
        { 
            get { return _itemPath; }
            set
            {
                // Safe to call Update since it only enacts a write if we did change something.
                // So assignments during load won't trigger a save.
                _itemPath = value;
                if (isSelected && exists && _itemPath != suggestedAssetPath)
                    SettingsManager.UpdateKnownItem(type, id, _itemPath);
            }
        }

        /// <summary>
        /// True if we're online and had selected an item from the kit or template list
        /// </summary>
        public bool isSelected => !string.IsNullOrEmpty(itemName);

        /// <summary>
        /// True if an asset path is set, regardless of validity
        /// </summary>
        public abstract bool isSet { get; }

        /// <summary>
        /// true if the path points to a valid asset
        /// </summary>
        public abstract bool exists { get; }

        /// <summary>
        /// Create or update this item (kit or template) on Altspace's website
        /// </summary>
        /// <returns></returns>
        public bool updateAltVRItem()
        {
            if (!WebClient.IsAuthenticated)
                return false;

            string new_id = LoginManager.ManageAltVRItem(this);
            if (id == null)
                id = new_id;

            return true;
        }

        /// <summary>
        /// Create the needed asset to store the item locally
        /// </summary>
        public abstract void createAsset();

        /// <summary>
        /// When using build&Upload, use this as the assetbundle's name
        /// </summary>
        public string bundleName { get { return id + "_" + type; } }

        /// <summary>
        /// Build an asset bundle out of this given item
        /// </summary>
        /// <param name="architectures">Architectures to build for</param>
        /// <param name="includeScreenshots">Include screenshots of items (kit only)</param>
        /// <param name="targetFileName">Where to put the .zip file</param>
        /// <returns></returns>
        public abstract bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null);

        /// <summary>
        /// Suggests the asset path if we find assets in the project that could belong to the selected Altspace item
        /// </summary>
        public abstract string suggestedAssetPath { get; }

        public abstract void importAltVRItem<U>(U json);

        /// <summary>
        /// Start the appropiate user interaction to choose the asset for the given Altspace item
        /// </summary>
        public abstract void chooseAssetPath();

        /// <summary>
        /// type: "kit" or "space_template"
        /// </summary>
        public abstract string type { get; }

        /// <summary>
        /// friendly name of the type, for display in UI
        /// </summary>
        public abstract string friendlyName { get; }        // "kit" or "template"

        /// <summary>
        /// plural of the friendly name
        /// </summary>
        public abstract string pluralName { get; }          // "kits" or "templates"

        /// <summary>
        /// Describe yourself in the GUI, with all details
        /// </summary>
        public abstract void showSelf();

        /// <summary>
        /// build the form data content necessary to upload the given item
        /// </summary>
        /// <returns>HTTP Content, ready to be posted</returns>
        public abstract HttpContent buildUploadContent(Parameters? parm = null);

        /// <summary>
        /// build the form data content necessary to create or edit a given item
        /// </summary>
        /// <param name="authtoken">The Auth Token we got from the landing page</param>
        /// <returns>HTTP Content, ready to be posted</returns>
        public virtual (string pattern, HttpContent inner) buildManageContent(string authtoken)
        {
            string commit_btn_playload = (id == null)
                ? "Create " + friendlyName.Capitalize()
                : "Update";

            string pattern = "data-method=\"delete\" href=\"/" + type + "s/";

            MultipartFormDataContent inner = new MultipartFormDataContent
                {
                    { new StringContent("✓"), "\"utf8\"" },
                    { new StringContent(authtoken), "\"authenticity_token\"" },
                    { new StringContent(LoginManager.userid), type+ "[current_user_id]" },
                    { new StringContent(itemName), type+ "[name]" },
                    { new StringContent(commit_btn_playload), "\"commit\"" },
                };

            if (id != null)
                inner.Add(new StringContent("patch"), "\"_method\"");

            if (!string.IsNullOrEmpty(description))
                inner.Add(new StringContent(description), type + "[description]");

            if (!string.IsNullOrEmpty(imageFile))
            {
                var imageFileContent = new ByteArrayContent(File.ReadAllBytes(imageFile));
                inner.Add(imageFileContent, type + "[image]", Path.GetFileName(imageFile));
            }
            //else
            //{
            //    var imageFileContent = new ByteArrayContent(new byte[0]);
            //    inner.Add(imageFileContent, type + "[image]");
            //}

            if (tag_list != null)
                inner.Add(new StringContent(tag_list), type + "[tag_list]");

            return (pattern, inner);
        }

        public virtual bool isAssetBundleItem { get => false; }

    }

}

#endif