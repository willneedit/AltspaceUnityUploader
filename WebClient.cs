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

        public class AltspaceRequest : HttpContent
        {
            protected byte[] _content;
            protected string _apiUrl;
            protected HttpMethod _method;

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

        public class PagedAssetRequest<T> : AltspaceRequest
        {
            private T _pagedAsset;
            private int _page = 0;
            private int _pages = 0;

            public T pagedAsset { get => _pagedAsset; }
            public int page { get => _page; }
            public int pages { get => _pages; }

            public PagedAssetRequest(int page)
            {
                string item_type_plural;
                if (typeof(T) == typeof(kitsJSON))
                    item_type_plural = "kits";
                else if (typeof(T) == typeof(templatesJSON))
                    item_type_plural = "space_templates";
                else
                    throw new InvalidDataException("Type " + typeof(T).Name + " unsupported");

                _apiUrl = "/api/" + item_type_plural + "/my.json?page=" + page;
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
                MultipartFormDataContent _inner = null;

                var zipContents = new ByteArrayContent(File.ReadAllBytes(uploadFileName));
                zipContents.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");

                var colorSpace = PlayerSettings.colorSpace == ColorSpace.Linear ? "linear" : "gamma";
                var srp = PlayerSettings.stereoRenderingPath == StereoRenderingPath.Instancing
                    ? (isExclusivelyAndroid ? "spmv" : "spi")
                    : (PlayerSettings.stereoRenderingPath == StereoRenderingPath.SinglePass)
                        ? "sp"
                        : "mp";

                _inner = new MultipartFormDataContent
                {
                    { new StringContent("" + Common.usingUnityVersion), item.type + "[game_engine_version]" },
                    { new StringContent(srp), item.type + "[stereo_render_mode]" },
                    { new StringContent(colorSpace), item.type + "[color_space]" },
                    { zipContents, item.type + "[zip]", item.bundleName + ".zip" }
                };

                _apiUrl = "/api/" + item.type + "s/" + item.id + ".json";
                _method = HttpMethod.Put;

                _content = _inner.ReadAsByteArrayAsync().Result;
                Headers.ContentType = _inner.Headers.ContentType;
                Headers.ContentDisposition = _inner.Headers.ContentDisposition;
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

        private static CookieContainer _cookieContainer = null;
        private static HttpClient _client = null;
        private static string _authToken;

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

        public static void ForgetAuthentication()
        {
            _cookieContainer = null;
            _authToken = null;
        }

        public static bool IsAuthenticated => _authToken != null;

        public static Task<HttpResponseMessage> SendAsync(AltspaceRequest request, UseAuthState ua = UseAuthState.UA_USE)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(request.method, request.apiUrl))
            {
                req.Headers.TryAddWithoutValidation("X-AppName", "AltspaceUnityUploader 2.0.0");

                if (!string.IsNullOrEmpty(_authToken) || (ua & UseAuthState.UA_IGNORE) != 0)
                    req.Headers.Add("X-Auth-Token", _authToken);

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
            using(var res = response.Result)
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
    }
}

#endif