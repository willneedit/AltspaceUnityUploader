#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineKitManager : EditorWindow
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


        private static Dictionary<string, AltspaceKitItem> _known_kits = new Dictionary<string, AltspaceKitItem>();
        private static AltspaceKitItem _selected_kit = new AltspaceKitItem();

        public static string kitRoot => _selected_kit.itemPath;

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
            else if (!kit.exists)
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

        public static void ResetContents()
        {
            OnlineKitManager window = GetWindow<OnlineKitManager>();
            window.Close();
            _known_kits = new Dictionary<string, AltspaceKitItem>();
            _selected_kit = new AltspaceKitItem();
        }

        private static void EnterKitData(kitJSON kit)
        {
            if (kit.name != null && kit.user_id == LoginManager.userid)
            {
                _known_kits.Remove(kit.kit_id);
                AltspaceKitItem new_item = new AltspaceKitItem();
                new_item.importAltVRItem(kit);
                _known_kits.Add(kit.kit_id, new_item);
            }
        }

        private static bool LoadSingleKit(string kit_id)
        {
            kitJSON kit = LoginManager.LoadSingleAltVRItem<kitJSON>(kit_id);
            if(kit != null && !string.IsNullOrEmpty(kit.name))
            {
                EnterKitData(kit);
                return true;
            }
            return false;
        }

        public static void ManageKits()
        {
            AltVRItemWidgets.ManageItem(
                _selected_kit,
                () => GetWindow<OnlineKitManager>().Show(),
                (string id) => { LoadSingleKit(id); _selected_kit = _known_kits[id]; },
                "You need to set a directory before you can build kits.");
        }

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {

            AltVRItemWidgets.BuildSelectorList(_known_kits.Values, CreateKit, LoadKits, SelectKit, ref m_scrollPosition);

            void SelectKit(string id)
            {
                _selected_kit = _known_kits[id];
                this.Close();
                GetWindow<LoginManager>().Repaint();
            }

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

                        if (new_item.updateAltVRItem() && LoadSingleKit(new_item.id))
                        {
                            _selected_kit = _known_kits[new_item.id];
                            _selected_kit.itemPath = Path.Combine(
                                SettingsManager.settings.KitsRootDirectory,
                                Common.SanitizeFileName(_selected_kit.itemName));

                            this.Close();
                            GetWindow<LoginManager>().Repaint();
                        }
                    });
                window.Show();
            }

            void LoadKits()
            {
                LoginManager.LoadAltVRItems((kitsJSON content) =>
                {
                    foreach (kitJSON kit in content.kits)
                        EnterKitData(kit);
                });

                if (_known_kits.Count == 0)
                    ShowNotification(new GUIContent("No own kits"), 5.0f);

            }

        }

    }
}

#endif // UNITY_EDITOR
