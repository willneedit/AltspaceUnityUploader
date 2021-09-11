using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    public interface IPaginated
    {
        paginationJSON pages { get; }
        // string assetType { get; }
        void iterator<U>(Action<U> callback);
    }

    public interface ITypedAsset
    {
        // string assetType { get; }
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
    /// Collection of AssetBundles, coined to a specific user, inside a template.
    /// </summary>
    [Serializable]
    public class assetBundleSceneJSON
    {
        public string user_id = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>();
    }

}
