#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineGLTFManager : OnlineManagerBase<AltspaceModelItem, modelJSON, OnlineGLTFManager>
    {
        private class CreateGLTFWindow : CreateWindowBase
        {
            public string modelName = "";
            public string description = "";
            public string imageFile = "";
            public string gltfFile = "";

            public void OnGUI()
            {

                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

                modelName = EditorGUILayout.TextField(new GUIContent("Model Name", "The name of the model"), modelName);
                EditorGUILayout.LabelField(new GUIContent("Model Description", "A description for the model"));
                description = EditorGUILayout.TextArea(description);

                gltfFile = Common.FileSelectionField(new GUIContent(
                    "GLB File",
                    "The binary GLTF (*.glb) file to upload"),
                    false, false, gltfFile);

                imageFile = Common.FileSelectionField(new GUIContent(
                    "Image File",
                    "Optional. An image to be shown in the overview"),
                    false, false, imageFile);

                Trailer(() => modelName != "" && gltfFile != "");
            }
        }


        public static string kitRoot => _selected_item.itemPath;

        public static void ShowModel(AltspaceModelItem model)
        {
            Common.ShowItem(model);

            if(model.isSelected)
            {
                Common.DisplayStatus("Created:", "never", Common.ParseTimeString(model.createdAt).ToLocalTime().ToShortDTString());
                Common.DisplayStatus("Updated:", "never", Common.ParseTimeString(model.updatedAt).ToLocalTime().ToShortDTString());
            }

            model.itemPath = Common.FileSelectionField(new GUIContent("GLB File:"), false, false, model.itemPath);

            if (!model.isSet) return;

            if(!model.itemPath.StartsWith("Assets"))
            {
                var style = new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(0.60f, 0.60f, 0);

                GUILayout.Label(
                    "This file is not within this project's Assets.\n"+
                    "It is recommended to keep the GLB file within this project\n", style);
            }
            else if (!model.exists)
            {
                GUILayout.Label("The file does exist.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            }
        }

        public static void ManageModels() => ManageItems("You need to set a valid GLB file before you can upload it.");

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {

            AltVRItemWidgets.BuildSelectorList(_known_items.Values, CreateKit, LoadItems<modelsJSON>, SelectItem, ref m_scrollPosition);

            void CreateKit()
            {
                CreateGLTFWindow window = CreateInstance<CreateGLTFWindow>();
                window.SetCommitAction(() =>
                    {
                        AltspaceModelItem new_item = new AltspaceModelItem
                        {
                            itemName = window.modelName,
                            description = window.description,
                            imageFile = window.imageFile,
                            itemPath = window.gltfFile
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
