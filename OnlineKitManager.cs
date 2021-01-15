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
            string result = LoginManager.CreateAltVRItem("kit", "kit", name, description, imageFileName);
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

        public static void ManageKits(EditorWindow parent)
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
                {
                    BuildKit();
                    parent.ShowNotification(new GUIContent("Kit creation finished"), 5.0f);
                }

                if (HasKitSelected)
                {
                    if (GUILayout.Button("Build & Upload"))
                    {
                        BuildAndUploadKit();
                        parent.ShowNotification(new GUIContent("Kit upload finished"), 5.0f);
                    }
                }

            }


            EditorGUILayout.EndHorizontal();
        }

        private static void BuildKit()
        {
            KitBuilder.BuildKitAssetBundle(SettingsManager.SelectedBuildTargets, true);
        }

        private static void BuildAndUploadKit()
        {
            List<BuildTarget> targets = SettingsManager.SelectedBuildTargets;

            string kitUploadDir = Common.CreateTempDirectory();

            Task<HttpResponseMessage> uploadTask = null;
            bool addScreenshots = true;

            foreach (BuildTarget target in targets)
            {
                if (uploadTask != null)
                {
                    HttpResponseMessage result = uploadTask.Result;
                    if (!result.IsSuccessStatusCode)
                    {
                        Debug.LogWarning("Error during kit upload:" + result.StatusCode);
                        Debug.LogWarning(result.Content.ReadAsStringAsync().Result);

                        // Continue with other architectures even if one failed.
                        // break;
                    }
                }

                uploadTask = null;

                string kitUploadFile = Path.Combine(kitUploadDir, "kitUpload");
                if (target == BuildTarget.StandaloneOSX)
                    kitUploadFile += "_Mac.zip";
                else if (target == BuildTarget.Android)
                    kitUploadFile += "_Android.zip";
                else
                    kitUploadFile += ".zip";

                List<BuildTarget> singleTarget = new List<BuildTarget>();
                singleTarget.Add(target);

                KitBuilder.BuildKitAssetBundle(singleTarget, addScreenshots, kitUploadFile);

                MultipartFormDataContent form = new MultipartFormDataContent();
                ByteArrayContent zipContents = new ByteArrayContent(File.ReadAllBytes(kitUploadFile));

                // Explicitly set content type
                zipContents.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                form.Add(zipContents, "kit[zip]", Path.GetFileName(OnlineKitManager.kitRoot.ToLower()) + ".zip");
                form.Add(new StringContent("" + Common.usingUnityVersion), "kit[game_engine_version]");

                uploadTask =
                    LoginManager.GetHttpClient().PutAsync("/api/kits/" + _selected_kit.kit_data.kit_id + ".json", form);

                addScreenshots = false;
            }

            // And wait for the final upload to be finished.
            if (uploadTask != null)
            {
                HttpResponseMessage result = uploadTask.Result;
                if (!result.IsSuccessStatusCode)
                    Debug.LogWarning("Error during kit upload:" + result.StatusCode);
            }

            Directory.Delete(kitUploadDir, true);

            // Reload kit data (and update display)
            LoadSingleKit(_selected_kit.kit_data.kit_id);
            _selected_kit = _known_kits[_selected_kit.kit_data.kit_id];

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
