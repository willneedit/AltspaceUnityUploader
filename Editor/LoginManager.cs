#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class LoginManager : EditorWindow
    {
        public static readonly string versionString = "3.0.0-beta2";

        private static string _login = "";
        private static string _password = "";
        private static userEntryJSON _userEntry = null;
        
        /// <summary>
        /// ID of the currently logged in user, null if not logged in or unavailable.
        /// </summary>
        public static string userid
        {
            get => _userEntry == null ? null : _userEntry.user_id;
        }

        /// <summary>
        /// Returns the HTTP Client (decorated with credential cookie, if available) if available
        /// </summary>
        /// <returns>Client if present, null otherwise</returns>
        public static HttpClient GetHttpClient() => WebClient.GetHttpClient();

        /// <summary>
        /// Create an item in AltspaceVR.
        /// </summary>
        /// <param name="id">ID of an existing item, null for a new one</param>
        /// <param name="item_singular">'space_template' or 'kit'</param>
        /// <param name="name">Name of item to create</param>
        /// <param name="description">The description</param>
        /// <param name="imageFileName">(Optional) Local file name of the image</param>
        /// 
        /// <returns>The ID of the generated item</returns>
        /// <param name="tag_list">(templates only) comma separated tag list</param>
        public static string ManageAltVRItem(string id, string item_singular, string name, string description, string imageFileName, string tag_list = null)
        {
            string item_fn = null;

            if (item_singular == "space_template")
                item_fn = "template";
            else if (item_singular == "kit")
                item_fn = "kit";
            else
                throw new InvalidDataException(item_singular + ": Not a recognized Altspace item");

            string progress_caption = (id == null)
                ? "Creating " + item_fn
                : "Updating " + item_fn;

            EditorUtility.DisplayProgressBar(progress_caption, "Retrieving landing page...", 0.0f);

            var ilpr = new WebClient.ItemLandingPageRequest(id, item_singular);
            if(!ilpr.Process())
            {
                EditorUtility.ClearProgressBar();
                return null;
            }

            EditorUtility.DisplayProgressBar(progress_caption, "Posting new item...", 0.5f);

            var imr = new WebClient.ItemManageRequest(ilpr.authtoken, id, item_singular, name, description, imageFileName, tag_list);
            imr.Process();

            EditorUtility.ClearProgressBar();
            return imr.id_result;
        }

        /// <summary>
        /// Load a single AltVR item (kit or template)
        /// </summary>
        /// <typeparam name="T">the data type in question</typeparam>
        /// <param name="item_id">The ID of the item within the given scope</param>
        /// <returns>The item, or null</returns>
        public static T LoadSingleAltVRItem<T>(string item_id) where T: ITypedAsset, new()
        {
            var sar = new WebClient.SingleAssetRequest<T>(item_id);
            if (!sar.Process()) return default;

            return sar.singleAsset;
        }

        public static void LoadAltVRItems<T>(Action<T> callback) where T: ITypedAsset, new()
        {
            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading item list", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                var par = new WebClient.PagedAssetsRequest<T>(currentPage);
                if(par.Process())
                {
                    maxPage = par.pages;
                    callback(par.pagedAsset);
                }
            }

            EditorUtility.ClearProgressBar();
        }

        public static void BuildAndUploadAltVRItem(List<BuildTarget> targets, AltspaceListItem item)
        {
            string itemUploadDir = Common.CreateTempDirectory();

            Task<HttpResponseMessage> uploadTask = null;
            bool addScreenshots = (item.type == "kit");

            foreach (BuildTarget target in targets)
            {
                if (uploadTask != null)
                {
                    HttpResponseMessage result = uploadTask.Result;
                    if (!result.IsSuccessStatusCode)
                    {
                        Debug.LogWarning("Error during " + item.type + " upload:" + result.StatusCode);
                        Debug.LogWarning(result.Content.ReadAsStringAsync().Result);

                        uploadTask = null;
                        break;
                    }
                }

                uploadTask = null;

                string itemUploadFile = Path.Combine(itemUploadDir, item.type + "Upload");
                if (target == BuildTarget.StandaloneOSX)
                    itemUploadFile += "_Mac.zip";
                else if (target == BuildTarget.Android)
                    itemUploadFile += "_Android.zip";
                else
                    itemUploadFile += ".zip";

                List<BuildTarget> singleTarget = new List<BuildTarget> { target };

                item.buildAssetBundle(singleTarget, addScreenshots, itemUploadFile);
                addScreenshots = false;

                var upr = new WebClient.UploadRequest(item, itemUploadFile, target == BuildTarget.Android);
                uploadTask = upr.ProcessAsync();
            }

            // And wait for the final upload to be finished.
            if (uploadTask != null)
            {
                HttpResponseMessage result = uploadTask.Result;
                if (!result.IsSuccessStatusCode)
                {
                    Debug.LogWarning("Error during " + item.type + " upload:" + result.StatusCode);
                    Debug.LogWarning(result.Content.ReadAsStringAsync().Result);
                }
            }

            Directory.Delete(itemUploadDir, true);
        }

        [MenuItem("AUU/Manage Login", false, 0)]
        public static void ShowLogInWindow()
        {
            LoginManager window = GetWindow<LoginManager>();
            window.Show();
        }

        private int m_selectedTab = 0;

        public void OnEnable()
        {
        }

        public void OnDestroy()
        {
        }

        public void OnGUI()
        {
            if (!SettingsManager.initialized)
            {
                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
                EditorGUILayout.Space(10);

                if (!String.IsNullOrEmpty(SettingsManager.initErrorMsg))
                {
                    GUIStyle style = new GUIStyle() { fontStyle = FontStyle.Bold, fontSize = 18 };
                    style.normal.textColor = new Color(0.80f, 0, 0);
                    EditorGUILayout.LabelField(SettingsManager.initErrorMsg, style);
                }
                else
                    EditorGUILayout.LabelField(new GUIContent("Initializing, please wait..."));
                EditorGUILayout.EndVertical();
                return;
            }

            WebClient.GetHttpClient();
            if(WebClient.IsAuthenticated && _userEntry == null)
            {
                var uidr = new WebClient.UserIDRequest();
                if (uidr.Process())
                    _userEntry = uidr.userEntry;
                else
                    WebClient.ForgetAuthentication();

            }

            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            Common.DisplayStatus("Login State:", "Logged out", WebClient.IsAuthenticated ? "Logged in" : null);

            if (WebClient.IsAuthenticated)
            {
                if (userid != null)
                {
                    Common.DisplayStatus("ID:", "unknown", userid);
                    Common.DisplayStatus("User handle:", "unknown", _userEntry.username);
                    Common.DisplayStatus("Display name:", "unknown", _userEntry.display_name);
                }
            }

            EditorGUILayout.Space();

            if (!WebClient.IsAuthenticated)
            {
                _login = EditorGUILayout.TextField(new GUIContent("EMail", "The EMail you've registered yourself to Altspace with."), _login);
                _password = EditorGUILayout.PasswordField(new GUIContent("Password", "Your password"), _password);

                if (GUILayout.Button("Log In"))
                    DoLogin();
            }
            else
            {
                if (GUILayout.Button("Log Out"))
                    DoLogout();


            }

            EditorGUILayout.Space();

            m_selectedTab = GUILayout.Toolbar(m_selectedTab, new string[] { "Kits", "Templates" });
            switch(m_selectedTab)
            {
                case 0: // Kits
                    OnlineKitManager.ManageKits();
                    break;
                case 1: // Templates
                    OnlineTemplateManager.ManageTemplates();
                    break;
                default:
                    break;
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Altspace Unity Uploader " + versionString, EditorStyles.centeredGreyMiniLabel);
            GUILayout.EndVertical();

        }


        private void DoLogin()
        {
            var req = new WebClient.LoginRequest(_login, _password);
            if(!req.Process())
                ShowNotification(new GUIContent("Login failed"), 5.0f);
        }

        private void DoLogout()
        {
            OnlineKitManager.ResetContents();
            OnlineTemplateManager.ResetContents();
            _userEntry = null;

            var req = new WebClient.LogoutRequest();
            if(!req.Process())
            {
                Debug.LogWarning("Logout failed");
            }

        }
    }
}

#endif // UNITY_EDITOR
