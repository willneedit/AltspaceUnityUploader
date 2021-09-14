#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEditor;

namespace AltSpace_Unity_Uploader
{
    /// <summary>
    /// A single GLTF model
    /// </summary>
    [Serializable]
    public class skyboxJSON : ITypedAsset
    {
        public string name = null;                  // The name
        public string id = null;                    // The ID
        public string three_sixty_image = null;     // The URL of the 360° image
        public string audio_url = null;             // The URL of the audio file
        public string created_at;                   // Creation date
        public string updated_at;                   // Last modification date

        public static string assetPluralType { get => "skyboxes"; }
        public string assetId { get => id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of GLTF models
    /// </summary>
    [Serializable]
    public class skyboxesJSON : IPaginated
    {
        public List<skyboxJSON> skyboxes = new List<skyboxJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "skyboxes"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (skyboxJSON item in skyboxes)
                (callback as Action<skyboxJSON>)(item);
        }
    }

    public class AltspaceSkyboxItem : AltspaceListItem
    {

        public string createdAt;
        public string updatedAt;
        public string bgAudioPath;

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "Skybox", fileName + ".png");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            skyboxJSON json = _json as skyboxJSON;
            itemName = json.name;
            id = json.id;
            item_url = json.three_sixty_image;
            tag_list = null;
            createdAt = json.created_at;
            updatedAt = json.updated_at;

            itemPath = SettingsManager.LookupKnownItem(type, id);
            if (itemPath == null)
                itemPath = suggestedAssetPath;
        }

        public override void chooseAssetPath()
        {
            string fileName = id + "_" + Common.SanitizeFileName(itemName);
            itemPath = Path.Combine("Assets", "Skybox", fileName + ".png");
        }

        public override void createAsset() => throw new NotImplementedException();

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            // There's nothing to build.
            return true;
        }

        public override void showSelf() => OnlineSkyboxManager.ShowSkybox(this);

        public override string type => "skybox";

        public override string friendlyName => "skybox";

        public override string pluralType => "skyboxes";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => throw new NotImplementedException();

        public override (string pattern, HttpContent inner) buildManageContent(string authtoken)
        {
            string pattern;
            HttpContent innerBase;
            (pattern, innerBase) = base.buildManageContent(authtoken);

            MultipartFormDataContent inner = innerBase as MultipartFormDataContent;

            if (!string.IsNullOrEmpty(itemPath))
            {
                var skyboxFileContent = new ByteArrayContent(File.ReadAllBytes(itemPath));
                inner.Add(skyboxFileContent, type + "[three_sixty_image]", "skybox" + Path.GetExtension(itemPath));
            }
            else
            {
                var skyboxFileContent = new ByteArrayContent(new byte[0]);
                inner.Add(skyboxFileContent, type + "[three_sixty_image]");
            }

            if(!String.IsNullOrEmpty(bgAudioPath))
            {
                var bgAudioContent = new ByteArrayContent(File.ReadAllBytes(bgAudioPath));
                inner.Add(bgAudioContent, type + "[audio]", "bgAudio" + Path.GetExtension(bgAudioPath));
            }

            return (pattern, inner);

        }
    }

}


#endif
