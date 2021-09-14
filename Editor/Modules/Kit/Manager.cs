#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineKitManager : OnlineManagerBase<AltspaceKitItem, kitJSON, OnlineKitManager>
    {
        private class CreateKitWindow : CreateWindowBase
        {
            public string kitName = "";
            public string description = "";
            public string imageFile = "";

            public void OnGUI()
            {

                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

                kitName = EditorGUILayout.TextField(new GUIContent("Kit Name", "The name of the kit"), kitName);
                EditorGUILayout.LabelField(new GUIContent("Kit Description", "A description for the kit"));
                description = EditorGUILayout.TextArea(description);

                imageFile = Common.FileSelectionField(new GUIContent(
                    "Image File",
                    "Optional. An image to be shown in the overview"),
                    false, false, imageFile);

                Trailer(() => kitName != "");
            }
        }


        public static string kitRoot => _selected_item.itemPath;

        public static void ShowKit(AltspaceKitItem kit)
        {
            Common.ShowItem(kit);

            kit.itemPath = Common.FileSelectionField(new GUIContent("Kit Prefab Directory:"), true, false, kit.itemPath);

            if (!kit.isSet) return;

            if(!kit.itemPath.StartsWith("Assets"))
            {
                var style = new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                style.normal.textColor = new Color(0.80f, 0, 0);

                GUILayout.Label(
                    "This directory is not within this project's Assets.\n"+
                    "You need to set a directory path to any place within the Assets of this project,\n"+
                    "preferably to a directory you solely dedicate to the future kit objects.", style);
            }
            if (!kit.exists)
            {
                GUILayout.Label("The directory doesn't exist.\nPress the button below to create it.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Create kit prefab directory"))
                {
                    kit.createAsset();
                    AssetDatabase.Refresh();
                }
            }
        }

        public static void ManageKits() => ManageItems("You need to set a directory before you can build kits.");

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {

            AltVRItemWidgets.BuildSelectorList(_known_items.Values, CreateKit, LoadItems<kitsJSON>, SelectItem, ref m_scrollPosition);

            void CreateKit()
            {
                CreateKitWindow window = CreateInstance<CreateKitWindow>();
                window.SetCommitAction(() =>
                    {
                        AltspaceKitItem new_item = new AltspaceKitItem()
                        {
                            itemName = window.kitName,
                            description = window.description,
                            imageFile = window.imageFile
                        };

                        if (new_item.updateAltVRItem() && LoadSingleItem(new_item.id))
                        {
                            _selected_item = _known_items[new_item.id];
                            _selected_item.itemPath = Path.Combine(
                                SettingsManager.settings.KitsRootDirectory,
                                Common.SanitizeFileName(_selected_item.itemName));

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
