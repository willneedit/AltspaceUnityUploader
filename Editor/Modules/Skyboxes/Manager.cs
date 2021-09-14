#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineSkyboxManager : OnlineManagerBase<AltspaceSkyboxItem, skyboxJSON, OnlineSkyboxManager>
    {
        private class CreateSkyboxWindow : CreateWindowBase
        {
            public string sbName = "";
            public string description = "";
            public string imageFile = "";
            public string threesixtyFile = "";
            public string acFile = "";

            public void OnGUI()
            {

                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

                sbName = EditorGUILayout.TextField(new GUIContent("Skybox Name", "The name of the skybox"), sbName);
                EditorGUILayout.LabelField(new GUIContent("Skybox Description", "A description for the skybox"));
                description = EditorGUILayout.TextArea(description);

                threesixtyFile = Common.FileSelectionField(new GUIContent(
                    "Skybox image",
                    "The 360° equirectangular image file which depicts the skybox"),
                    false, false, threesixtyFile);

                acFile = Common.FileSelectionField(new GUIContent(
                    "Background audio file",
                    "(Optional) The background audio file to upload. Supported formats are\n.wav, .ogg, .mp3, .mp4, .m4a, .flac"),
                    false, false, acFile);

                imageFile = Common.FileSelectionField(new GUIContent(
                    "Image File",
                    "Optional. An image to be shown in the overview"),
                    false, false, imageFile);

                Trailer(() => sbName != "" && threesixtyFile != "");
            }
        }


        public static void ShowSkybox(AltspaceSkyboxItem skybox)
        {
            Common.ShowItem(skybox);

            if(skybox.isSelected)
            {
                Common.DisplayStatus("Created:", "never", Common.ParseTimeString(skybox.createdAt).ToLocalTime().ToShortDTString());
                Common.DisplayStatus("Updated:", "never", Common.ParseTimeString(skybox.updatedAt).ToLocalTime().ToShortDTString());
            }

            skybox.itemPath = Common.FileSelectionField(new GUIContent("Skybox image File:"), false, false, skybox.itemPath);

            if (skybox.isSet)
            {
                if (!skybox.itemPath.StartsWith("Assets"))
                {
                    var style = new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    style.normal.textColor = new Color(0.60f, 0.60f, 0);

                    GUILayout.Label(
                        "This file is not within this project's Assets.\n" +
                        "It is recommended to keep the image file within this project\n", style);
                }
                if (!skybox.exists)
                {
                    GUILayout.Label("The file does exist.", new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });
                }
            }


            skybox.bgAudioPath = Common.FileSelectionField(new GUIContent("Background Audio File:"), false, false, skybox.bgAudioPath);
            if(!string.IsNullOrEmpty(skybox.bgAudioPath))
            {
                if (!skybox.bgAudioPath.StartsWith("Assets"))
                {
                    var style = new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    style.normal.textColor = new Color(0.60f, 0.60f, 0);

                    GUILayout.Label(
                        "This file is not within this project's Assets.\n" +
                        "It is recommended to keep the image file within this project\n", style);
                }
                if (!File.Exists(skybox.bgAudioPath))
                {
                    GUILayout.Label("The file does exist.", new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });
                }
            }
        }

        public static void ManageSkyboxes() => ManageItems("You need to set a valid skybox image file before you can upload it.");

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {

            AltVRItemWidgets.BuildSelectorList(_known_items.Values, CreateKit, LoadItems<skyboxesJSON>, SelectItem, ref m_scrollPosition);

            void CreateKit()
            {
                CreateSkyboxWindow window = CreateInstance<CreateSkyboxWindow>();
                window.SetCommitAction(() =>
                    {
                        AltspaceSkyboxItem new_item = new AltspaceSkyboxItem
                        {
                            itemName = window.sbName,
                            description = window.description,
                            imageFile = window.imageFile,
                            itemPath = window.threesixtyFile,
                            bgAudioPath = window.acFile
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
