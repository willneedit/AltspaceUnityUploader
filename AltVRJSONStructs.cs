using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
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
        public int page;
        public int pages;
        public int count;
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
    public class kitJSON
    {
        public string name = null;
        public bool is_featured = false;
        public string kit_id = null;
        public string user_id = null;
        public List<assetBundleJSON> asset_bundles = new List<assetBundleJSON>();
    }

    /// <summary>
    /// A single page of kits
    /// </summary>
    [Serializable]
    public class kitsJSON
    {
        public List<kitJSON> kits = new List<kitJSON>();
        public paginationJSON pagination;
    }
}
