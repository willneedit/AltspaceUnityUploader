#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    /// <summary>
    /// A single kit
    /// </summary>
    [Serializable]
    public class kitJSON : ITypedAsset
    {
        public string name = null;
        public bool is_featured = false;
        public string kit_id = null;
        public string user_id = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>();

        public static string assetPluralType { get => "kits"; }
        public string assetId { get => kit_id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of kits
    /// </summary>
    [Serializable]
    public class kitsJSON : IPaginated
    {
        public List<kitJSON> kits = new List<kitJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "kits"; }
        public void iterator<U>(Action<U> callback)
        {
            foreach (kitJSON item in kits)
                if (item.user_id == LoginManager.userid)
                    (callback as Action<kitJSON>)(item);
        }
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

            if (includeScreenshots && !Directory.Exists(screenshotSrc))
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

        public override string pluralType => "kits";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && Directory.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.BundleContent(this, parm.Value);

        public override bool isAssetBundleItem { get => true; }
    }


}


#endif
