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
        public static readonly string versionString = "1.0.0";

        private static string _login = null;
        private static string _password = null;
        private static userEntryJSON _userEntry = null;
        private static bool _trieduserEntry = false;
        private static CookieContainer _cookieContainer = null;
        private static HttpClient _client = null;
        
        /// <summary>
        /// ID of the currently logged in user, null if not logged in or unavailable.
        /// </summary>
        public static string userid
        {
            get
            {
                if (!IsConnected) return null;

                if (_userEntry == null && !_trieduserEntry)
                {
                    HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync("api/users/me.json").Result;
                    if (result.IsSuccessStatusCode)
                    {
                        string res = result.Content.ReadAsStringAsync().Result;
                        userListJSON l = JsonUtility.FromJson<userListJSON>(res);
                        if (l != null && l.users.Count > 0)
                            _userEntry = l.users[0];
                    }
                    _trieduserEntry = true;
                }

                return _userEntry == null ? null : _userEntry.user_id;
            }
        }

        /// <summary>
        /// Returns the HTTP Client (decorated with credential cookie, if available) if available
        /// </summary>
        /// <returns>Client if present, null otherwise</returns>
        public static HttpClient GetHttpClient()
        {
            if (_cookieContainer == null)
            {
                _cookieContainer = new CookieContainer();
                _client = null;
            }

            if (_client != null) return _client;

            HttpClientHandler handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
            _client = new HttpClient(handler) { BaseAddress = new System.Uri("https://account.altvr.com") };

            return _client;
        }

        /// <summary>
        /// true if the user is logged in (credential cookie present)
        /// </summary>
        public static bool IsConnected { get { return _cookieContainer != null; } }

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

            string item_plural = item_singular + "s";
            string progress_caption = (id == null)
                ? "Creating " + item_fn
                : "Updating " + item_fn;

            string commit_btn_playload = (id == null)
                ? "Create " + item_fn.Substring(0, 1).ToUpper() + item_fn.Substring(1)
                : "Update";

            string id_result = null;
            string authtoken;

            string auth_token_pattern = "type=\"hidden\" name=\"authenticity_token\" value=\"";
            string template_id_pattern = "data-method=\"delete\" href=\"/" + item_plural + "/";

            EditorUtility.DisplayProgressBar(progress_caption, "Retrieving landing page...", 0.0f);

            try
            {
                string landing_uri = (id == null)
                    ? item_plural + "/new"
                    : item_plural + "/" + id + "/edit";

                HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync(landing_uri).Result;
                string content = result.Content.ReadAsStringAsync().Result;
                result.EnsureSuccessStatusCode();

                authtoken = Common.GetWebParameter(content, auth_token_pattern);
            }
            catch (Exception e)
            {
                Debug.LogWarning(e.Message);
                EditorUtility.ClearProgressBar();
                return null;
            }

            EditorUtility.DisplayProgressBar(progress_caption, "Posting new " + item_fn + "...", 0.5f);

            try
            {
                MultipartFormDataContent form = new MultipartFormDataContent();
                ByteArrayContent imageFileContent = null;

                // Web server is not completely standards compliant and requires the form-data headers in the form
                // name="itemname", rather than also accepting name=itemname.
                // .NET HttpClient only uses quotes when necessary. Bummer.
                form.Add(new StringContent("✓"), "\"utf8\"");
                form.Add(new StringContent(authtoken), "\"authenticity_token\"");
                form.Add(new StringContent(name), item_singular + "[name]");
                form.Add(new StringContent(description), item_singular + "[description]");

                if (!String.IsNullOrEmpty(imageFileName))
                {
                    imageFileContent = new ByteArrayContent(File.ReadAllBytes(imageFileName));
                    form.Add(imageFileContent, item_singular + "[image]", Path.GetFileName(imageFileName));
                }
                else
                {
                    imageFileContent = new ByteArrayContent(new byte[0]);
                    form.Add(imageFileContent, item_singular + "[image]");
                }

                if (tag_list != null)
                    form.Add(new StringContent(tag_list), item_singular + "[tag_list]");

                form.Add(new StringContent(commit_btn_playload), "\"commit\"");

                HttpResponseMessage result = LoginManager.GetHttpClient().PostAsync(item_plural, form).Result;
                string content = result.Content.ReadAsStringAsync().Result;
                result.EnsureSuccessStatusCode();

                id_result = Common.GetWebParameter(content, template_id_pattern);

            }
            catch (HttpRequestException)
            {
                Debug.LogError("HTTP Request error");
            }
            catch (FileNotFoundException)
            {
                Debug.LogError("Image file not found");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return id_result;
        }

        /// <summary>
        /// Load a single AltVR item (kit or template)
        /// </summary>
        /// <typeparam name="T">the data type in question</typeparam>
        /// <param name="item_id">The ID of the item within the given scope</param>
        /// <returns>The item, or null</returns>
        public static T LoadSingleAltVRItem<T>(string item_id)
        {
            string item_type_plural;

            if (typeof(T) == typeof(kitJSON))
                item_type_plural = "kits";
            else if (typeof(T) == typeof(templateJSON))
                item_type_plural = "space_templates";
            else
                throw new InvalidDataException("Type " + typeof(T).Name + " unsupported");

            try
            {
                HttpResponseMessage result = GetHttpClient().GetAsync("api/" + item_type_plural + "/" + item_id).Result;
                result.EnsureSuccessStatusCode();

                return JsonUtility.FromJson<T>(result.Content.ReadAsStringAsync().Result);
            }
            catch (HttpRequestException)
            {
                Debug.LogError("Error reading Altspace item");
            }

            return default;
        }

        public static void LoadAltVRItems<T>(Action<T> callback)
        {
            string item_type_plural;

            if (typeof(T) == typeof(kitsJSON))
                item_type_plural = "kits";
            else if (typeof(T) == typeof(templatesJSON))
                item_type_plural = "space_templates";
            else
                throw new InvalidDataException("Type " + typeof(T).Name + " unsupported");

            int currentPage = 0;
            int maxPage = 1;

            while (currentPage < maxPage)
            {
                EditorUtility.DisplayProgressBar("Reading kit descriptions", "Loading page... (" + currentPage + "/" + maxPage + ")", currentPage / maxPage);

                currentPage++;

                try
                {
                    HttpResponseMessage result = LoginManager.GetHttpClient().GetAsync("api/" + item_type_plural + "/my.json?page=" + currentPage).Result;
                    result.EnsureSuccessStatusCode();

                    T kitsPage = JsonUtility.FromJson<T>(result.Content.ReadAsStringAsync().Result);
                    IPaginated p = (IPaginated)kitsPage;

                    if (kitsPage == null)
                        Debug.LogError("Completely malformed JSON");
                    else if (p.pages == null)
                        Debug.LogWarning("Pagination information missing -- assuming single page");
                    else
                    {
                        maxPage = p.pages.pages;
                        callback(kitsPage);

                    }
                }
                catch (HttpRequestException)
                {

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

                List<BuildTarget> singleTarget = new List<BuildTarget>();
                singleTarget.Add(target);

                item.buildAssetBundle(singleTarget, addScreenshots, itemUploadFile);

                MultipartFormDataContent form = new MultipartFormDataContent();
                ByteArrayContent zipContents = new ByteArrayContent(File.ReadAllBytes(itemUploadFile));

                // Explicitly set content type
                zipContents.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                form.Add(zipContents, item.type + "[zip]", item.bundleName + ".zip");
                form.Add(new StringContent("" + Common.usingUnityVersion), item.type + "[game_engine_version]");

                uploadTask =
                    LoginManager.GetHttpClient().PutAsync("/api/" + item.type + "s/" + item.id + ".json", form);

                addScreenshots = false;
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

                EditorGUILayout.LabelField(new GUIContent("Initializing, please wait..."));
                EditorGUILayout.EndVertical();
                return;
            }

            if (_login == null || _password == null)
                RevertLoginData();

            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            Common.DisplayStatus("Login State:", "Logged out", IsConnected ? "Logged in" : null);

            if (IsConnected)
            {
                if (userid != null)
                {
                    Common.DisplayStatus("ID:", "unknown", userid);
                    Common.DisplayStatus("User handle:", "unknown", _userEntry.username);
                    Common.DisplayStatus("Display name:", "unknown", _userEntry.display_name);
                }
            }

            EditorGUILayout.Space();

            if (!IsConnected)
            {
                _login = EditorGUILayout.TextField(new GUIContent("EMail", "The EMail you've registered yourself to Altspace with."), _login);
                _password = EditorGUILayout.PasswordField(new GUIContent("Password", "Your password"), _password);

                if (GUILayout.Button(new GUIContent("Reread login credentials", "Revert back to the login credentials in the settings")))
                    RevertLoginData();

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


        private void RevertLoginData()
        {
            Settings s = SettingsManager.settings;

            _login = s.Login;
            _password = s.Password;

            Repaint();
        }

        private void DoLogin()
        {
            var parameters = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("user[email]", _login),
                new KeyValuePair<string, string>("user[password]", _password)
            });

            try
            {
                HttpResponseMessage result = GetHttpClient().PostAsync("/users/sign_in.json", parameters).Result;
                result.EnsureSuccessStatusCode();
                _trieduserEntry = false;
                _userEntry = null;
                //foreach(Cookie cookie in _cookieContainer.GetCookies(new System.Uri("https://account.altvr.com")))
                //{
                //    Debug.Log("Cookie: " + cookie.Name + "=" + cookie.Value);
                //}
            }
            catch (HttpRequestException)
            {
                ShowNotification(new GUIContent("Login failed"), 5.0f);
                _cookieContainer = null;
            }
        }

        private void DoLogout()
        {
            OnlineKitManager.ResetContents();
            OnlineTemplateManager.ResetContents();

            try
            {
                HttpResponseMessage result = GetHttpClient().DeleteAsync("/users/sign_out.json").Result;
                result.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                Debug.LogWarning("Logout failed");
            }

            _cookieContainer = null;
        }
    }
}

#endif // UNITY_EDITOR
