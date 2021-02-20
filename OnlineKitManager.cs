#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineKitManager : EditorWindow
    {

        private static Dictionary<string, AltspaceKitItem> _known_kits = new Dictionary<string, AltspaceKitItem>();
        private static AltspaceKitItem _selected_kit = new AltspaceKitItem();

        public static bool HasLoadedKits => _known_kits.Count > 0;

        public static string kitRoot => _selected_kit.itemPath;

        public static void ShowSelectedKit()
        {
            if(LoginManager.IsConnected)
            {
                EditorGUILayout.LabelField("Selected Kit:");
                Common.DisplayStatus("  Name:", "none", _selected_kit.itemName);
                Common.DisplayStatus("  ID:", "none", _selected_kit.id);
            }

            if (_selected_kit.isSelected)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Kit contents:");
                Common.DescribeAssetBundles(_selected_kit.asset_bundles);

            }

            _selected_kit.itemPath = Common.FileSelectionField(new GUIContent("Kit Prefab Directory:"), true, false, _selected_kit.itemPath);
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

        private void LoadKits()
        {
            LoginManager.LoadAltVRItems((kitsJSON content) =>
            {
                foreach (kitJSON kit in content.kits)
                    EnterKitData(kit);
            });

            if(_known_kits.Count == 0)
                ShowNotification(new GUIContent("No own kits"), 5.0f);

        }

        public static void ManageKits()
        {
            if (LoginManager.IsConnected)
            {
                if (GUILayout.Button("Select Kit"))
                    ShowKitSelection();
            }
            else
                GUILayout.Label("Offline mode", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

            EditorGUILayout.Space(10);

            ShowSelectedKit();

            if(_selected_kit.isSet && !_selected_kit.exists)
            {
                GUILayout.Label("The directory doesn't exist.\nPress the button below to create it.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Create kit prefab directory"))
                {
                    _selected_kit.createAsset();
                    AssetDatabase.Refresh();
                }
            }


            EditorGUILayout.BeginHorizontal();

            if(!_selected_kit.isSet)
                GUILayout.Label("You need to set a directory before you can build kits.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if(_selected_kit.exists)
            {
                if (GUILayout.Button("Build"))
                    EditorApplication.update += BuildKit;

                if (_selected_kit.isSelected)
                {
                    if (GUILayout.Button("Build & Upload"))
                        EditorApplication.update += BuildAndUploadKit;
                }

            }


            EditorGUILayout.EndHorizontal();
        }

        private static void BuildKit()
        {
            EditorApplication.update -= BuildKit;
            string state = _selected_kit.buildAssetBundle(SettingsManager.SelectedBuildTargets, true) ? "finished" : "canceled";
            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Kit creation " + state), 5.0f);

        }

        private static void BuildAndUploadKit()
        {
            EditorApplication.update -= BuildAndUploadKit;

            string item_id = _selected_kit.id;

            LoginManager.BuildAndUploadAltVRItem(SettingsManager.SelectedBuildTargets, _selected_kit);

            // Reload kit data (and update display)
            LoadSingleKit(item_id);
            _selected_kit = _known_kits[item_id];

            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Kit upload finished"), 5.0f);

        }


        public static void ShowKitSelection()
        {
            OnlineKitManager window = GetWindow<OnlineKitManager>();
            window.Show();
        }

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            if (HasLoadedKits)
            {
                m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);
                foreach (var kit in _known_kits)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));

                    EditorGUILayout.LabelField(kit.Value.itemName);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                    {
                        _selected_kit = _known_kits[kit.Value.id];
                        this.Close();
                        GetWindow<LoginManager>().Repaint();
                    }

                    EditorGUILayout.EndHorizontal();
                }

                GUILayout.EndScrollView();
            }
            else
            {
                GUILayout.Label(
                    "No kits loaded. Either press \"Load kits\"\n" +
                    "to load known kits from the account,\n" +
                    "Or press \"Create New Kit\" to create a new one.", new GUIStyle() { fontStyle = FontStyle.Bold });
            }

            if (GUILayout.Button("Load Kits"))
                LoadKits();

            if (GUILayout.Button("Create New Kit"))
            {
                CreateKitWindow window = CreateInstance<CreateKitWindow>();
                window.ShowModalUtility();
                if (window.rc)
                {
                    AltspaceKitItem new_item = new AltspaceKitItem()
                    {
                        itemName = window.kitName,
                        description = window.description,
                        imageFile = window.imageFile
                    };

                    if(new_item.updateAltVRItem() && LoadSingleKit(new_item.id))
                    {
                        _selected_kit = _known_kits[new_item.id];
                        _selected_kit.itemPath = Path.Combine(
                            SettingsManager.settings.KitsRootDirectory,
                            Common.SanitizeFileName(_selected_kit.itemName));

                        this.Close();
                        GetWindow<LoginManager>().Repaint();
                    }
                }
            }
            GUILayout.EndVertical();
        }

    }
}

#endif // UNITY_EDITOR
