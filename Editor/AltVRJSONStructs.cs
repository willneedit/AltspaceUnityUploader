using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    public interface IPaginated
    {
        paginationJSON pages { get; }
    }

    public interface ITypedAsset
    {
        string assetType { get; }
        string assetId { get; }
        string assetName { get; }
    }

    [Serializable]
    public class userPwCredentialsJSON
    {
        public string email;
        public string password;
    }

    [Serializable]
    public class userLoginJSON
    {
        public userPwCredentialsJSON user = new userPwCredentialsJSON();
    }

    /// <summary>
    /// Part of a user entry
    /// </summary>
    [Serializable]
    public class userEntryJSON
    {
        public List<string> roles = new List<string>();
        public List<string> platform_roles = new List<string>();
        public string username;
        public string display_name;
        public string email;
        public string user_id;
    }

    [Serializable]
    public class userListJSON
    {
        public List<userEntryJSON> users = new List<userEntryJSON>();
    }

    /// <summary>
    /// Current page, number of pages
    /// </summary>
    [Serializable]
    public class paginationJSON
    {
        public int page = 0;
        public int pages = 0;
        public int count = 0;
    }

    /// <summary>
    /// An Asset Bundle inside a kit or template.
    /// </summary>
    [Serializable]
    public class assetBundleJSON
    {
        public string game_engine;
        public int game_engine_version;
        public string platform;
        public string created_at;
        public string updated_at;
    }

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

        public string assetType { get => "kit";  }
        public string assetId { get => kit_id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of kits
    /// </summary>
    [Serializable]
    public class kitsJSON : IPaginated, ITypedAsset
    {
        public List<kitJSON> kits = new List<kitJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public string assetType { get => "kit"; }
        public string assetId { get => null; }
        public string assetName { get => null; }
    }


    /// <summary>
    /// Collection of AssetBundles, coined to a specific user, inside a template.
    /// </summary>
    [Serializable]
    public class assetBundleSceneJSON
    {
        public string user_id = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>();
    }

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

        public string assetType { get => "space_template"; }
        public string assetId { get => space_template_id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of templates
    /// </summary>
    [Serializable]
    public class templatesJSON : IPaginated, ITypedAsset
    {
        public List<templateJSON> space_templates = new List<templateJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public string assetType { get => "space_template"; }
        public string assetId { get => null; }
        public string assetName { get => null; }
    }

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

        public string assetType { get => "model"; }
        public string assetId { get => id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of GLTF models
    /// </summary>
    [Serializable]
    public class modelsJSON : IPaginated, ITypedAsset
    {
        public List<modelJSON> models = new List<modelJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public string assetType { get => "model"; }
        public string assetId { get => null; }
        public string assetName { get => null; }
    }
}
