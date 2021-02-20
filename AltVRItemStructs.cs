#if UNITY_EDITOR

using System.Collections;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace AltSpace_Unity_Uploader
{
    public abstract class AltspaceListItem
    {
        private string _itemPath = null;

        public string itemName = null;      // Name of the online Altspace item, raw version. Null if no selection
        public string id = null;            // ID. Null if no selection
        public List<assetBundleJSON> asset_bundles = null;         // Asset Bundles connected to the Altspace Item

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
            if (!LoginManager.IsConnected)
                return false;

            string new_id = LoginManager.ManageAltVRItem(id, type, itemName, description, imageFile, tag_list);
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

        public void importAltVRItem(kitJSON json)
        {
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

            // Gather assets and create asset bundle for the given architecture
            string[] assetFiles = Directory.GetFiles(itemPath);
            string[] screenshotFiles = includeScreenshots ? Directory.GetFiles(screenshotSrc) : new string[0];
            string tgtRootName = isSelected
                ? bundleName
                : Common.SanitizeFileName(Path.GetFileName(itemPath)).ToLower();

            targetFileName = Common.BuildAssetBundle(assetFiles, screenshotFiles, architectures, tgtRootName, targetFileName);
            return targetFileName != null;
        }

        public override string type => "kit";

        public override string friendlyName => "kit";

        public override string pluralName => "kits";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && Directory.Exists(itemPath);
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

        public void importAltVRItem(templateJSON json)
        {
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

        public override string type => "space_template";

        public override string friendlyName => "template";

        public override string pluralName => "templates";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);
    }
}

#endif