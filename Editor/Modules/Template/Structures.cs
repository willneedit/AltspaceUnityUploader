#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AltSpace_Unity_Uploader
{
    /// <summary>
    /// A single template
    /// </summary>
    [Serializable]
    public class templateJSON : ITypedAsset
    {
        public string space_template_sid = null;    // Long sid
        public string activity_name = null;         // friendly name
        public string description = null;
        public string space_template_id = null;     // ID used in URLs
        public List<assetBundleSceneJSON> asset_bundle_scenes = new List<assetBundleSceneJSON>(); // asset Bundles coined to different users? Strange.
        public string name = null;                  // friendly name (again)

        public static string assetPluralType { get => "space_templates"; }
        public string assetId { get => space_template_id; }
        public string assetName { get => name; }

    }

    /// <summary>
    /// A single page of templates
    /// </summary>
    [Serializable]
    public class templatesJSON : IPaginated
    {
        public List<templateJSON> space_templates = new List<templateJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "space_templates"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (templateJSON item in space_templates)
                if (item.asset_bundle_scenes.Count > 0 && item.asset_bundle_scenes[0].user_id == LoginManager.userid)
                    (callback as Action<templateJSON>)(item);
        }
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

        public override string pluralType => "templates";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.BundleContent(this, parm.Value);
        public override bool isAssetBundleItem { get => true; }
    }


}


#endif
