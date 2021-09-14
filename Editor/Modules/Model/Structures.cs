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
    public class modelJSON : ITypedAsset
    {
        public string name = null;                  // The name
        public string id = null;                    // The ID
        public string gltf_url = null;              // The URL of the GLB file
        public string created_at;                   // Creation date
        public string updated_at;                   // Last modification date

        public static string assetPluralType { get => "models"; }
        public string assetId { get => id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of GLTF models
    /// </summary>
    [Serializable]
    public class modelsJSON : IPaginated
    {
        public List<modelJSON> models = new List<modelJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetPluralType { get => "models"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (modelJSON item in models)
                (callback as Action<modelJSON>)(item);
        }
    }

    public class AltspaceModelItem : AltspaceListItem
    {

        public string createdAt;
        public string updatedAt;

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "Models", fileName + ".glb");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            modelJSON json = _json as modelJSON;
            itemName = json.name;
            id = json.id;
            item_url = json.gltf_url;
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
            itemPath = Path.Combine("Assets", "Models", fileName + ".glb");
        }

        public override void createAsset() => throw new NotImplementedException();

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            // There's nothing to build.
            return true;
        }

        public override void showSelf() => OnlineGLTFManager.ShowModel(this);

        public override string type => "model";

        public override string friendlyName => "model";

        public override string pluralType => "models";

        public override bool isSet => !string.IsNullOrEmpty(itemPath);

        public override bool exists => isSet && File.Exists(itemPath);

        public override HttpContent buildUploadContent(Parameters? parm = null) => UploadContentMethods.GLTFContent(this);

        public override (string pattern, HttpContent inner) buildManageContent(string authtoken)
        {
            string pattern;
            HttpContent innerBase;
            (pattern, innerBase) = base.buildManageContent(authtoken);

            MultipartFormDataContent inner = innerBase as MultipartFormDataContent;

            if (!string.IsNullOrEmpty(itemPath))
            {
                var gltfFileContent = new ByteArrayContent(File.ReadAllBytes(itemPath));
                inner.Add(gltfFileContent, type + "[gltf]", "model.glb");
            }
            else
            {
                var gltfFileContent = new ByteArrayContent(new byte[0]);
                inner.Add(gltfFileContent, type + "[gltf]");
            }

            return (pattern, inner);

        }
    }

}


#endif
