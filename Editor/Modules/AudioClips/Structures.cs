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
    public class audioClipJSON : ITypedAsset
    {
        public string name = null;                  // The name
        public string id = null;                    // The ID
        public string audio_url = null;             // The URL of the audio file
        public string created_at;                   // Creation date
        public string updated_at;                   // Last modification date

        public static string assetType { get => "audio_clip"; }
        public string assetId { get => id; }
        public string assetName { get => name; }
    }

    /// <summary>
    /// A single page of GLTF models
    /// </summary>
    [Serializable]
    public class audioClipsJSON : IPaginated
    {
        public List<audioClipJSON> audio_clips = new List<audioClipJSON>();
        public paginationJSON pagination = new paginationJSON();

        public paginationJSON pages { get => pagination; }

        public static string assetType { get => "audio_clip"; }

        public void iterator<U>(Action<U> callback)
        {
            foreach (audioClipJSON item in audio_clips)
                (callback as Action<audioClipJSON>)(item);
        }
    }

    public class AltspaceAudioClipItem : AltspaceListItem
    {

        public string createdAt;
        public string updatedAt;

        public override string suggestedAssetPath
        {
            get
            {
                string fileName = id + "_" + Common.SanitizeFileName(itemName);
                string fullName = Path.Combine("Assets", "AudioClip", fileName + ".wav");
                return (File.Exists(fullName)) ? fullName : null;
            }
        }

        public override void importAltVRItem<U>(U _json)
        {
            audioClipJSON json = _json as audioClipJSON;
            itemName = json.name;
            id = json.id;
            item_url = json.audio_url;
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
            itemPath = Path.Combine("Assets", "AudioClip", fileName + ".wav");
        }

        public override void createAsset() => throw new NotImplementedException();

        public override bool buildAssetBundle(List<BuildTarget> architectures, bool includeScreenshots = false, string targetFileName = null)
        {
            // There's nothing to build.
            return true;
        }

        public override void showSelf() => OnlineAudioClipManager.ShowAudioClip(this);

        public override string type => "audio_clip";

        public override string friendlyName => "audio clip";

        public override string pluralName => "audio_clips";

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
                var audioClipFileContent = new ByteArrayContent(File.ReadAllBytes(itemPath));
                inner.Add(audioClipFileContent, type + "[audio]", "audio_clip." + Path.GetExtension(itemPath));
            }
            else
            {
                var audioClipFileContent = new ByteArrayContent(new byte[0]);
                inner.Add(audioClipFileContent, type + "[audio]");
            }

            return (pattern, inner);

        }
    }

}


#endif
