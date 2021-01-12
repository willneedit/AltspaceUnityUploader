#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class LoginManager : EditorWindow
    {
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
                    OnlineKitManager.ManageKits(this);
                    break;
                case 1: // Templates
                    // ManageTemplates();
                    break;
                default:
                    break;
            }

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
