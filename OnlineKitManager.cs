using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        private static string _selected_kit = null;
        private static string _selected_kitroot = null;
        private Vector2 _scrollPosition;

        public static bool HasLoadedKits { get { return _known_kits.Count > 0; } }


        public static void ShowSelectedKit()
        {
            if(LoginManager.IsConnected)
                Common.DisplayStatus("Selected Kit:", "none", _selected_kit);

            if(_selected_kit != null)
            {
                GUILayout.Space(10);

                kitInfo kit = null;
                if(_known_kits.TryGetValue(_selected_kit, out kit))
                {
                    GUILayout.Label("Kit contents:");
                    Common.DescribeAssetBundles(kit.kit_data.asset_bundles);
                }
                else
                    Common.DisplayStatus("Kit contents:", "Not loaded", null);

            }

            _selected_kitroot = Common.FileSelectionField(new GUIContent("Kit Prefab Directory:"), true, false, _selected_kitroot);
        }

        public static void ResetContents()
        {
            OnlineKitManager window = GetWindow<OnlineKitManager>();
            window.Close();
            _known_kits = new Dictionary<string, kitInfo>();
            _selected_kit = null;
            _selected_kitroot = null;
        }

        private Dictionary<string, string> knownKitDirectories = new Dictionary<string, string>();

        private static string GetSuggestedKitDirectory(string kit_id, string kit_name)
        {
            return SettingsManager.settings.KitsRootDirectory + "/" + kit_id + "_" + Common.SanitizeFileName(kit_name);
        }

        private string GetKnownKitDirectory(string kit_id, string kit_name)
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
            string kit_id = null;
            string authtoken = null;

            string auth_token_pattern = "type=\"hidden\" name=\"authenticity_token\" value=\"";
            string kit_id_pattern = "data-method=\"delete\" href=\"/kits/";

            EditorUtility.DisplayProgressBar("Creating kit", "Retrieving landing page...", 0.0f);

            try
            {
                HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync("kits/new").Result;
                string content = result.Content.ReadAsStringAsync().Result;
                result.EnsureSuccessStatusCode();

                authtoken = Common.GetWebParameter(content, auth_token_pattern);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                ShowNotification(new GUIContent("Cannot use landing page"), 5.0f);
                EditorUtility.ClearProgressBar();
                return null;
            }


            //EditorUtility.ClearProgressBar();
            //return null;

            EditorUtility.DisplayProgressBar("Creating kit", "Posting new kit...", 0.5f);

            try
            {
                MultipartFormDataContent form = new MultipartFormDataContent();
                ByteArrayContent imageFileContent = null;

                // Web server is not completely standards compliant and requires the form-data headers in the form
                // name="itemname", rather than also accepting name=itemname.
                // .NET HttpClient only uses quotes when necessary. Bummer.
                form.Add(new StringContent("✓"), "\"utf8\"");
                form.Add(new StringContent(authtoken), "\"authenticity_token\"");
                form.Add(new StringContent(name), "kit[name]");
                form.Add(new StringContent(description), "kit[description]");

                if(!String.IsNullOrEmpty(imageFileName))
                {
                    imageFileContent = new ByteArrayContent(File.ReadAllBytes(imageFileName));
                    form.Add(imageFileContent, "kit[image]", Path.GetFileName(imageFileName));
                }
                else
                {
                    imageFileContent = new ByteArrayContent(new byte[0]);
                    form.Add(imageFileContent, "kit[image]");
                }

                form.Add(new StringContent("Create Kit"), "\"commit\"");

                HttpResponseMessage result = LoginManager.GetHttpClient().PostAsync("kits", form).Result;
                string content = result.Content.ReadAsStringAsync().Result;
                result.EnsureSuccessStatusCode();

                kit_id = Common.GetWebParameter(content, kit_id_pattern);

                ShowNotification(new GUIContent("Kit registered"), 5.0f);
            }
            catch (HttpRequestException)
            {
                ShowNotification(new GUIContent("Kit register failed"), 5.0f);
            }
            catch(FileNotFoundException)
            {
                ShowNotification(new GUIContent("Image not found"), 5.0f);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }


            return kit_id;
        }

        private void EnterKitData(kitJSON kit)
        {
            if (kit.name != null && kit.user_id == LoginManager.userid)
            {
                _known_kits.Remove(kit.name);
                _known_kits.Add(kit.name, new kitInfo()
                {
                    kitroot_directory = GetKnownKitDirectory(kit.kit_id, kit.name),
                    kit_data = kit
                });
            }
        }

        private bool LoadSingleKit(string kit_id)
        {
            try
            {
                HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync("api/kits/" + kit_id).Result;
                result.EnsureSuccessStatusCode();

                kitJSON kit = JsonUtility.FromJson<kitJSON>(result.Content.ReadAsStringAsync().Result);

                if (String.IsNullOrEmpty(kit.name))
                    return false;
                    
                EnterKitData(kit);
                return true;
            }
            catch (HttpRequestException)
            {

            }

            return false;
        }
        private void LoadKits()
        {
            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading kit descriptions", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                try
                {
                    HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync("api/kits/my.json?page=" + currentPage).Result;
                    result.EnsureSuccessStatusCode();

                    kitsJSON kitsPage = JsonUtility.FromJson<kitsJSON>(result.Content.ReadAsStringAsync().Result);
                    if (kitsPage == null)
                        Debug.LogError("Completely malformed JSON");
                    else if (kitsPage.pagination == null)
                        Debug.LogWarning("Pagination information missing -- assuming single page");
                    else
                        maxPage = kitsPage.pagination.pages;

                    if (kitsPage.kits == null)
                        Debug.LogWarning("Page doesn't seem to contain kits");
                    else
                        foreach (kitJSON kit in kitsPage.kits)
                            EnterKitData(kit);
                }
                catch (HttpRequestException)
                {

                }
            }

            EditorUtility.ClearProgressBar();
            if(_known_kits.Count == 0)
                ShowNotification(new GUIContent("No own kits"), 5.0f);

        }

        public static void ShowKitSelection()
        {
            OnlineKitManager window = GetWindow<OnlineKitManager>();
            window.Show();
        }

        public void OnEnable()
        {
        }

        public void OnDestroy()
        {
        }

        public void OnGUI()
        {
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            if(HasLoadedKits)
            {
                _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
                foreach (var kit in _known_kits)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));

                    EditorGUILayout.LabelField(kit.Key);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                    {
                        _selected_kit = kit.Key;
                        _selected_kitroot = kit.Value.kitroot_directory;
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
                if(window.rc)
                {
                    string kit_id = CreateKit(window.kitName, window.description, window.imageFile);
                    if (LoadSingleKit(kit_id))
                    {
                        _selected_kit = window.kitName;
                        _selected_kitroot = GetKnownKitDirectory(_selected_kit, window.kitName);
                    }
                }
            }
            // CreateKit("__AUUTest", "This is a test for the AUU kit creation", "D:/Users/carsten/Pictures/Sweet-Fullscene.png");
            GUILayout.EndVertical();
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

            string kit_id = (_selected_kit != null) ? _known_kits[_selected_kit].kit_data.kit_id : "";

            bool hasKitRootSelected = _selected_kitroot != null && _selected_kitroot != "";
            bool existsKitRoot = hasKitRootSelected && Directory.Exists(_selected_kitroot);
            bool isStandardKitRoot =
                hasKitRootSelected
                && (_selected_kit == null ||
                GetSuggestedKitDirectory(kit_id, _selected_kit) == _selected_kitroot);

            if(hasKitRootSelected && !isStandardKitRoot)
            {
                GUILayout.Label("The directory name doesn't match the standard format.\nRenaming the directory is recommended. ", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Rename kit prefab directory"))
                {
                    string new_kitroot = GetSuggestedKitDirectory(kit_id, _selected_kit);
                    if (existsKitRoot)
                    {
                        File.Delete(_selected_kitroot + ".meta");
                        Directory.Move(_selected_kitroot, new_kitroot);
                    }
                    _selected_kitroot = new_kitroot;
                    AssetDatabase.Refresh();
                }
            }
            if(hasKitRootSelected && !existsKitRoot)
            {
                GUILayout.Label("The directory doesn't exist.\nPress the button below to create it.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
                if (GUILayout.Button("Create kit prefab directory"))
                {
                    Directory.CreateDirectory(_selected_kitroot);
                    AssetDatabase.Refresh();
                }
            }


            EditorGUILayout.BeginHorizontal();

            if(!hasKitRootSelected)
                GUILayout.Label("You need to set a directory before you can build kits.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if(existsKitRoot)
            {
                if (GUILayout.Button("Build"))
                    _ = 1; // KitBuild();

                if(_selected_kit != null)
                {
                    if (GUILayout.Button("Build & Upload"))
                        _ = 1; //KitBuildUpload();
                }

            }


            EditorGUILayout.EndHorizontal();
        }
    }
}