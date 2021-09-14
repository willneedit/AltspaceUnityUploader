#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineAudioClipManager : OnlineManagerBase<AltspaceAudioClipItem, audioClipJSON, OnlineAudioClipManager>
    {
        private class CreateGLTFWindow : CreateWindowBase
        {
            public string acName = "";
            public string description = "";
            public string imageFile = "";
            public string acFile = "";

            public void OnGUI()
            {

                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

                acName = EditorGUILayout.TextField(new GUIContent("Audio Clip Name", "The name of the audio clip"), acName);
                EditorGUILayout.LabelField(new GUIContent("Audio Clip Description", "A description for the audio clip"));
                description = EditorGUILayout.TextArea(description);

                acFile = Common.FileSelectionField(new GUIContent(
                    "Audio Clip File",
                    "The audio clip file to upload. Supported formats are\n.wav, .ogg, .mp3, .mp4, .m4a, .flac"),
                    false, false, acFile);

                imageFile = Common.FileSelectionField(new GUIContent(
                    "Image File",
                    "Optional. An image to be shown in the overview"),
                    false, false, imageFile);

                Trailer(() => acName != "" && acFile != "");
            }
        }


        public static void ShowAudioClip(AltspaceAudioClipItem audioClip)
        {
            Common.ShowItem(audioClip);

            if(audioClip.isSelected)
            {
                Common.DisplayStatus("Created:", "never", Common.ParseTimeString(audioClip.createdAt).ToLocalTime().ToShortDTString());
                Common.DisplayStatus("Updated:", "never", Common.ParseTimeString(audioClip.updatedAt).ToLocalTime().ToShortDTString());
            }

            audioClip.itemPath = Common.FileSelectionField(new GUIContent("Audio Clip File:"), false, false, audioClip.itemPath);

            if (!audioClip.isSet) return;

            if(!audioClip.itemPath.StartsWith("Assets"))
            {
                var style = new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(0.60f, 0.60f, 0);

                GUILayout.Label(
                    "This file is not within this project's Assets.\n"+
                    "It is recommended to keep the audio clip file within this project\n", style);
            }
            if (!audioClip.exists)
            {
                GUILayout.Label("The file does exist.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }

        public static void ManageAudioClips() => ManageItems("You need to set a valid audio clip file before you can upload it.");

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {

            AltVRItemWidgets.BuildSelectorList(_known_items.Values, CreateKit, LoadItems<audioClipsJSON>, SelectItem, ref m_scrollPosition);

            void CreateKit()
            {
                CreateGLTFWindow window = CreateInstance<CreateGLTFWindow>();
                window.SetCommitAction(() =>
                    {
                        AltspaceAudioClipItem new_item = new AltspaceAudioClipItem
                        {
                            itemName = window.acName,
                            description = window.description,
                            imageFile = window.imageFile,
                            itemPath = window.acFile
                        };

                        if (new_item.updateAltVRItem() && LoadSingleItem(new_item.id))
                        {
                            _selected_item = _known_items[new_item.id];
                            _selected_item.itemPath = _selected_item.suggestedAssetPath;

                            this.Close();
                            GetWindow<LoginManager>().Repaint();
                        }
                    });
                window.Show();
            }

        }

    }
}

#endif // UNITY_EDITOR
