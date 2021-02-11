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

        public class kitInfo
        {
            public string kitroot_directory = null;
            public kitJSON kit_data = new kitJSON();
        }

        private static Dictionary<string, kitInfo> _known_kits = new Dictionary<string, kitInfo>();
        private static kitInfo _selected_kit = new kitInfo() { kitroot_directory = "" };

        public static bool HasLoadedKits { get { return _known_kits.Count > 0; } }
        public static bool HasKitRootSelected { get { return !String.IsNullOrEmpty(_selected_kit.kitroot_directory); } }
        public static bool HasKitSelected { get { return _selected_kit.kit_data.kit_id != null; } }

        public static string kitRoot {  get { return _selected_kit.kitroot_directory; } }

        public static void ShowSelectedKit()
        {
            if(LoginManager.IsConnected)
            {
                EditorGUILayout.LabelField("Selected Kit:");
                Common.DisplayStatus("  Name:", "none", _selected_kit.kit_data.name);
                Common.DisplayStatus("  ID:", "none", _selected_kit.kit_data.kit_id);
            }

            if (HasKitSelected)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Kit contents:");
                Common.DescribeAssetBundles(_selected_kit.kit_data.asset_bundles);

            }

            _selected_kit.kitroot_directory = Common.FileSelectionField(new GUIContent("Kit Prefab Directory:"), true, false, _selected_kit.kitroot_directory);
        }

        public static void ResetContents()
        {
            OnlineKitManager window = GetWindow<OnlineKitManager>();
            window.Close();
            _known_kits = new Dictionary<string, kitInfo>();
            _selected_kit = new kitInfo();
        }

        private static Dictionary<string, string> knownKitDirectories = new Dictionary<string, string>();

        private static string GetSuggestedKitDirectory(string kit_id, string kit_name)
        {
            return SettingsManager.settings.KitsRootDirectory + "/" + kit_id + "_" + Common.SanitizeFileName(kit_name).ToLower();
        }

        private static string GetKnownKitDirectory(string kit_id, string kit_name)
        {
            string path;

            if (!knownKitDirectories.TryGetValue(kit_id, out path))
                path = GetSuggestedKitDirectory(kit_id, kit_name);

            knownKitDirectories.Remove(kit_id);
            knownKitDirectories[kit_id] = path;

            return path;
        }

        private string CreateKit(string name, string description, string imageFileName)
        {
            string result = LoginManager.CreateAltVRItem("kit", name, description, imageFileName);
            ShowNotification(new GUIContent(
                "Kit registration " + ((result != null)
                ? "successful"
                : "failed")));
            return result;
        }

        private static void EnterKitData(kitJSON kit)
        {
            if (kit.name != null && kit.user_id == LoginManager.userid)
            {
                _known_kits.Remove(kit.kit_id);
                _known_kits.Add(kit.kit_id, new kitInfo()
                {
                    kitroot_directory = GetKnownKitDirectory(kit.kit_id, kit.name),
                    kit_data = kit
                });
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

            string kit_id = _selected_kit.kit_data.kit_id;

            bool existsKitRoot = HasKitRootSelected && Directory.Exists(kitRoot);
            bool isStandardKitRoot =
                HasKitRootSelected
                && (kit_id == null ||
                GetSuggestedKitDirectory(kit_id, _selected_kit.kit_data.name) == kitRoot);

            if(HasKitRootSelected && !isStandardKitRoot)
            {
                GUILayout.Label("The directory name doesn't match the standard format.\nRenaming the directory is recommended. ", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Rename kit prefab directory"))
                {
                    string new_kitroot = GetSuggestedKitDirectory(kit_id, _selected_kit.kit_data.name);
                    if (existsKitRoot)
                    {
                        File.Delete(kitRoot + ".meta");
                        Directory.Move(kitRoot, new_kitroot);
                    }
                    _selected_kit.kitroot_directory = new_kitroot;
                    AssetDatabase.Refresh();
                }
            }
            if(HasKitRootSelected && !existsKitRoot)
            {
                GUILayout.Label("The directory doesn't exist.\nPress the button below to create it.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Create kit prefab directory"))
                {
                    Directory.CreateDirectory(kitRoot);
                    AssetDatabase.Refresh();
                }
            }


            EditorGUILayout.BeginHorizontal();

            if(!HasKitRootSelected)
                GUILayout.Label("You need to set a directory before you can build kits.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if(existsKitRoot)
            {
                if (GUILayout.Button("Build"))
                    EditorApplication.update += BuildKit;

                if (HasKitSelected)
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
            string state = KitBuilder.BuildKitAssetBundle(SettingsManager.SelectedBuildTargets, true) ? "finished" : "canceled";
            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Kit creation " + state), 5.0f);

        }

        private static void BuildAndUploadKit()
        {
            EditorApplication.update -= BuildAndUploadKit;

            List<BuildTarget> targets = SettingsManager.SelectedBuildTargets;
            string item_type_singular = "kit";
            string itemRootName = Path.GetFileName(kitRoot.ToLower());
            string item_id = _selected_kit.kit_data.kit_id;

            LoginManager.BuildAndUploadAltVRItem(targets, item_type_singular, itemRootName, item_id);

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

                    EditorGUILayout.LabelField(kit.Value.kit_data.name);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                    {
                        _selected_kit = _known_kits[kit.Value.kit_data.kit_id];
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
                    string kit_id = CreateKit(window.kitName, window.description, window.imageFile);
                    if (LoadSingleKit(kit_id))
                        _selected_kit = _known_kits[kit_id];
                }
            }
            // CreateKit("__AUUTest", "This is a test for the AUU kit creation", "D:/Users/carsten/Pictures/Sweet-Fullscene.png");
            GUILayout.EndVertical();
        }

    }
}

#endif // UNITY_EDITOR
