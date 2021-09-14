#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    static class WebClient
    {
        public enum UseAuthState
        {
            UA_USE      = 0x0000,   // Default: Use (if present) and keep
            UA_IGNORE   = 0x0001,   // Ignore for this request
            UA_FORGET   = 0x0002    // Forget after this request
        }

        [Serializable]
        public class cookieJSON
        {
            public cookieJSON(string _name, string _value)
            {
                name = _name;
                value = _value;
            }

            public string name;
            public string value;
        }

        [Serializable]
        public class sessionStateJSON
        {
            public List<cookieJSON> cookies = new List<cookieJSON>();
            public string auth_token;
        }

        public class AltspaceRequest : HttpContent
        {
            protected byte[] _content;
            protected string _apiUrl;
            protected HttpMethod _method;
            protected string _referer;

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                return stream.WriteAsync(content, 0, content.Length);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = content.Length;
                return true;
            }

            protected void SetJSONContent<T>(T json)
            {
                string contentString = JsonUtility.ToJson(json);
                _content = System.Text.Encoding.UTF8.GetBytes(contentString);
                Headers.ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf8");
            }

            public bool hasContent => _content != null && _content.Length > 0;

            public string apiUrl { get => _apiUrl; }
            public HttpMethod method { get => _method; }
            public byte[] content { get => _content; }
            public string referer { get => _referer; }

            public virtual bool Process()
            {
                return Send(this);
            }

            public virtual Task<HttpResponseMessage> ProcessAsync()
            {
                throw new NotImplementedException();
            }
        }

        public class LoginRequest : AltspaceRequest
        {
            public LoginRequest(string email, string password)
            {
                userLoginJSON l = new userLoginJSON();
                l.user.email = email;
                l.user.password = password;
                SetJSONContent(l);

                _apiUrl = "/users/sign_in.json";
                _method = HttpMethod.Post;
            }

            public override bool Process()
            {
                if (!Send(this)) return false;

                CreateSessionDataFile();

                return true;
            }

        }

        public class LogoutRequest : AltspaceRequest
        {
            public LogoutRequest()
            {
                _apiUrl = "/users/sign_out.json";
                _method = HttpMethod.Delete;
            }

            public override bool Process()
            {
                bool ok = Send(this, UseAuthState.UA_FORGET);

                // Maybe the response to the Delete request returned the invalid auth token again?
                ForgetAuthentication();
                return ok;
            }
        }

        public class UserIDRequest : AltspaceRequest
        {
            private userEntryJSON _userentry;

            public userEntryJSON userEntry { get => _userentry; }

            public UserIDRequest()
            {
                _apiUrl = "/api/users/me.json";
                _method = HttpMethod.Get;
            }

            public override bool Process()
            {
                string content;
                if (!Send(this, out content)) return false;

                userListJSON l = JsonUtility.FromJson<userListJSON>(content);
                if (l == null || l.users.Count < 1)
                    return false;

                _userentry = l.users[0];
                return true;

            }
        }

        public class SingleAssetRequest<T> : AltspaceRequest where T: ITypedAsset, new()
        {
            private T _singleAsset;

            public T singleAsset { get => _singleAsset; }

            public SingleAssetRequest(string asset_id)
            {
                _apiUrl = "/api/" + DeriveWebTypeName<T>() + "/" + asset_id;
                _method = HttpMethod.Get;
            }

            public override bool Process()
            {
                string content;
                if (!Send(this, out content)) return false;

                _singleAsset = JsonUtility.FromJson<T>(content);
                return true;
            }
        }

        public class PagedAssetsRequest<T> : AltspaceRequest where T : IPaginated, new()
        {
            private T _pagedAsset;
            private int _page = 0;
            private int _pages = 0;

            public T pagedAsset { get => _pagedAsset; }
            public int page { get => _page; }
            public int pages { get => _pages; }

            public PagedAssetsRequest(int page)
            {
                _apiUrl = "/api/" + DeriveWebTypeName<T>() + "/my.json?page=" + page;
                _method = HttpMethod.Get;
            }


            public override bool Process()
            {
                string content;
                if (!Send(this, out content)) return false;
                _pagedAsset = JsonUtility.FromJson<T>(content);
                IPaginated p = (IPaginated)_pagedAsset;
                _page = p.pages.page;
                _pages = p.pages.pages;
                return true;
            }
        }

        public class UploadRequest : AltspaceRequest
        {
            public UploadRequest(AltspaceListItem item, string uploadFileName, bool isExclusivelyAndroid = false)
            {
                var inner = item.buildUploadContent(new AltspaceListItem.Parameters {
                    uploadFileName = uploadFileName,
                    isExclusivelyAndroid = isExclusivelyAndroid
                });
                _content = inner.ReadAsByteArrayAsync().Result;
                Headers.ContentType = inner.Headers.ContentType;
                Headers.ContentDisposition = inner.Headers.ContentDisposition;

                _apiUrl = "/api/" + item.pluralType + "/" + item.id + ".json";
                _method = HttpMethod.Put;
            }

            public UploadRequest(AltspaceListItem item)
            {
                var inner = item.buildUploadContent();
                _content = inner.ReadAsByteArrayAsync().Result;
                Headers.ContentType = inner.Headers.ContentType;
                Headers.ContentDisposition = inner.Headers.ContentDisposition;

                _apiUrl = "/api/" + item.pluralType + "/" + item.id + ".json";
                _method = HttpMethod.Put;
            }

            public override Task<HttpResponseMessage> ProcessAsync()
            {
                return SendAsync(this);
            }

            public override bool Process()
            {
                throw new NotImplementedException();
            }
        }

        public class ItemLandingPageRequest : AltspaceRequest
        {
            private string _authtoken = null;
            public string authtoken { get => _authtoken; }

            public ItemLandingPageRequest(AltspaceListItem item)
            {
                _apiUrl = "/" + item.pluralType + "/" + ((item.id == null)
                    ? "new"
                    : item.id + "/edit");
                _method = HttpMethod.Get;
            }

            public override bool Process()
            {
                string result;
                if (!Send(this, out result)) return false;

                string auth_token_pattern = "type=\"hidden\" name=\"authenticity_token\" value=\"";
                _authtoken = Common.GetWebParameter(result, auth_token_pattern);
                return true;
            }
        }

        public class ItemManageRequest : AltspaceRequest
        {
            private string _template_id_pattern;
            private string _id_result = null;

            public string id_result { get => _id_result; }

            public ItemManageRequest(string authtoken, AltspaceListItem item)
            {
                HttpContent inner;
                (_template_id_pattern, inner) = item.buildManageContent(authtoken);

                _content = inner.ReadAsByteArrayAsync().Result;
                Headers.ContentType = inner.Headers.ContentType;
                Headers.ContentDisposition = inner.Headers.ContentDisposition;

                _referer = "/" + item.pluralType + "/" + ((item.id == null)
                    ? "new"
                    : item.id + "/edit");

                if (item.id != null)
                    _apiUrl = "/" + item.pluralType + "/" + item.id;
                else
                    _apiUrl = "/" + item.pluralType;

                _method = HttpMethod.Post;
            }

            public override bool Process()
            {
                string result;
                if (!Send(this, out result)) return false;

                _id_result = Common.GetWebParameter(result, _template_id_pattern);
                return true;
            }
        }

        private static CookieContainer _cookieContainer = null;
        private static HttpClient _client = null;
        private static string _authToken;

        private static string _sessionDataFile = "Assets/AUU_User_Session.json";


        public static CookieContainer cookieContainer { get => _cookieContainer; }

        /// <summary>
        /// Derive the name needed for the API URL from the type of the JSON structure we expect to get.
        /// </summary>
        /// <typeparam name="T">anything that provides the assetPluralType static property</typeparam>
        /// <returns>"kits", "space_templates" ...</returns>
        private static string DeriveWebTypeName<T>()
        {
            // Dark Magic: Since the needed runtime doesn't really support interface definitions for static
            // class members, we access the static methods using Reflection.

            return (string)
                typeof(T).InvokeMember(
                    "assetPluralType",
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Static | 
                    System.Reflection.BindingFlags.GetProperty,
                    null, null, null);
        }

        public static HttpClient GetHttpClient()
        {
            if (_cookieContainer == null)
            {
                _cookieContainer = new CookieContainer();
                _client = null;
                if (SettingsManager.settings.RememberLogin && File.Exists(_sessionDataFile))
                {
                    string json = File.ReadAllText(_sessionDataFile);
                    DeSerializeSessionState(json);
                }
            }

            if (_client != null) return _client;

            HttpClientHandler handler = new HttpClientHandler() { CookieContainer = _cookieContainer };
            _client = new HttpClient(handler) { BaseAddress = new System.Uri("https://account.altvr.com") };

            return _client;
        }

        public static void ForgetAuthentication()
        {
            _cookieContainer = null;
            _authToken = null;
            DeleteSessionDataFile();
        }

        public static void CreateSessionDataFile()
        {
            if (SettingsManager.settings.RememberLogin)
            {
                string json = SerializeSessionState();
                File.WriteAllText(_sessionDataFile, json);
            }
        }
        public static void DeleteSessionDataFile()
        {
            File.Delete(_sessionDataFile);
            File.Delete(_sessionDataFile + ".meta");
        }

        public static bool IsAuthenticated => _authToken != null;

        public static Task<HttpResponseMessage> SendAsync(AltspaceRequest request, UseAuthState ua = UseAuthState.UA_USE)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(request.method, request.apiUrl))
            {
                req.Headers.TryAddWithoutValidation("X-AppName", "AltspaceUnityUploader 2.0.0");

                if (!string.IsNullOrEmpty(_authToken) || (ua & UseAuthState.UA_IGNORE) != 0)
                    req.Headers.Add("X-Auth-Token", _authToken);

                if (!string.IsNullOrEmpty(request.referer))
                    req.Headers.Referrer = new System.Uri("https://account.altvr.com" + request.referer);

                if (request.hasContent)
                    req.Content = request;

                var res = GetHttpClient().SendAsync(req, HttpCompletionOption.ResponseContentRead);

                if ((ua & UseAuthState.UA_FORGET) != 0)
                    ForgetAuthentication();

                return res;
            }
        }

        public static bool ReadResponse(Task<HttpResponseMessage> response, out string contentString)
        {
            contentString = "";
            try
            {
                using (var res = response.Result)
                {
                    if (!res.IsSuccessStatusCode)
                    {
                        Debug.LogErrorFormat("[{0}] {1}", res.StatusCode, res.Content.ReadAsStringAsync().Result);
                        return false;
                    }

                    if (res.Headers.TryGetValues("X-Auth-Token", out IEnumerable<string> tokens))
                        _authToken = tokens.First();

                    contentString = res.Content.ReadAsStringAsync().Result;
                    return true;
                }
            }
            catch (OperationCanceledException ex)
            {
                Debug.LogWarning($"Task has been canceled, possible timeout {ex.Message}");
                return false;
            }
        }

        public static bool Send(AltspaceRequest request, out string contentString, UseAuthState ua = UseAuthState.UA_USE)
        {
            var res = SendAsync(request, ua);
            return ReadResponse(res, out contentString);
        }

        public static bool Send(AltspaceRequest request, UseAuthState ua = UseAuthState.UA_USE)
        {
            var res = SendAsync(request, ua);
            return ReadResponse(res, out _);
        }

        public static string SerializeSessionState()
        {
            sessionStateJSON sj = new sessionStateJSON();
            sj.auth_token = _authToken;
            foreach (Cookie cookie in cookieContainer.GetCookies(new System.Uri("https://account.altvr.com")))
                sj.cookies.Add(new cookieJSON(cookie.Name, cookie.Value));

            return JsonUtility.ToJson(sj);
        }

        public static void DeSerializeSessionState(string sjJSON)
        {
            var client = GetHttpClient();

            sessionStateJSON sj = JsonUtility.FromJson<sessionStateJSON>(sjJSON);
            _authToken = sj.auth_token;

            foreach(cookieJSON cj in sj.cookies)
            {
                Cookie c = new Cookie(cj.name, cj.value, "/", "account.altvr.com");
                cookieContainer.Add(c);
            }
        }
    }
}

#endif