#if UNITY_EDITOR

using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System;

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

        public virtual void importAltVRItem<U>(U json) { }

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

    }

    public class AltspaceKitItem : AltspaceListItem
    {
        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName).ToLower();
                string fullName = Path.Combine(SettingsManager.settings.KitsRootDirectory, fileName);
                return (Directory.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            kitJSON json = _json as kitJSON;

            itemName = json.name;
            id = json.kit_id;
            description = null; // json.description;
            tag_list = null;
            asset_bundles = json.asset_bundles;

            itemPath = SettingsManager.LookupKnownItem(type, id);
            if (itemPath == null)
                itemPath = suggestedAssetPath;
        }

        public override void chooseAssetPath()
        {
            itemPath = Common.OpenFileDialog(SettingsManager.settings.KitsRootDirectory, true, false, "");
        }

        public override void createAsset()
        {
            Directory.CreateDirectory(itemPath);
        }

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            string screenshotSrc = Path.Combine(itemPath, "Screenshots");

            if(includeScreenshots && !Directory.Exists(screenshotSrc))
            {
                Debug.LogWarning("The screenshot directory is missing - no screenshots will be uploaded.");
                includeScreenshots = false;
            }

            // Gather assets and create asset bundle for the given architecture
            string[] assetFiles = Directory.GetFiles(itemPath);
            string[] screenshotFiles = includeScreenshots ? Directory.GetFiles(screenshotSrc) : new string[0];
            string tgtRootName = isSelected
                ? bundleName
                : Common.SanitizeFileName(Path.GetFileName(itemPath)).ToLower();

            targetFileName = Common.BuildAssetBundle(assetFiles, screenshotFiles, architectures, tgtRootName, targetFileName);
            return targetFileName != null;
        }

        public override void showSelf() => OnlineKitManager.ShowKit(this);

        public override string type => "kit";

        public override string friendlyName => "kit";

        public override string pluralName => "kits";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && Directory.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.BundleContent(this, parm.Value);
    }

    public class AltspaceTemplateItem : AltspaceListItem
    {

        public string templateSceneName => Path.GetFileNameWithoutExtension(itemPath);

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "Scenes", fileName + ".unity");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            templateJSON json = _json as templateJSON;
            itemName = json.name;
            id = json.space_template_id;
            description = json.description;
            tag_list = null;
            asset_bundles = json.asset_bundle_scenes[0].asset_bundles;

            itemPath = SettingsManager.LookupKnownItem(type, id);
            if (itemPath == null)
                itemPath = suggestedAssetPath;
        }

        public override void chooseAssetPath()
        {
            string sceneName = EditorSceneManager.GetActiveScene().name;
            itemPath = Path.Combine("Assets", "Scenes", sceneName + ".unity");
        }

        public override void createAsset()
        {
            Scene sc = EditorSceneManager.GetActiveScene();
            EditorSceneManager.SaveScene(sc, itemPath);
        }

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            string tmpSceneAssetName = TemplateBuilder.BuildTemplateScene();

            string[] assetFiles = new string[] {
                tmpSceneAssetName
            };
            string[] screenshotFiles = new string[0];
            string tgtRootName = isSelected
                ? bundleName
                : Common.SanitizeFileName(templateSceneName).ToLower();

            targetFileName = Common.BuildAssetBundle(assetFiles, screenshotFiles, architectures, tgtRootName, targetFileName);

            EditorSceneManager.OpenScene(itemPath);
            File.Delete(tmpSceneAssetName + ".meta");
            File.Delete(tmpSceneAssetName);
            AssetDatabase.Refresh();

            return targetFileName != null;
        }

        public override void showSelf() => OnlineTemplateManager.ShowTemplate(this);

        public override string type => "space_template";

        public override string friendlyName => "template";

        public override string pluralName => "templates";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.BundleContent(this, parm.Value);
    }

    public class AltspaceModelItem : AltspaceListItem
    {

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "Models", fileName + ".glb");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public void importAltVRItem(modelJSON json)
        {
            itemName = json.name;
            id = json.id;
            item_url = json.gltf_url;
            tag_list = null;

            itemPath = SettingsManager.LookupKnownItem(type, id);
            if (itemPath == null)
                itemPath = suggestedAssetPath;
        }

        public override void chooseAssetPath()
        {
            string fileName = id + "_" + Common.SanitizeFileName(itemName);
            itemPath = Path.Combine("Assets", "Models", fileName + ".glb");
        }

        public override void createAsset()
        {
            throw new NotImplementedException();
        }

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            // There's nothing to build.
            return true;
        }

        public override void showSelf() => Common.ShowItem(this);

        public override string type => "model";

        public override string friendlyName => "model";

        public override string pluralName => "models";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.GLTFContent(this);
    }
}

#endif