#if UNITY_EDITOR

using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    public class Common
    {
        public static readonly int currentUnityVersion = 20194;

        public static readonly string strictUnityVersion = "2019.4.2f1";

        private static int _usingUnityVersion = 0;

        public static int usingUnityVersion { get
            {
                if (_usingUnityVersion == 0)
                {
                    string[] parts = Application.unityVersion.Split('.');
                    int.TryParse(parts[0] + parts[1], out _usingUnityVersion);
                }

                return _usingUnityVersion;
            }
        }
        public static void DisplayStatus(string caption, string defaultText, string activeText, string goodText = null)
        {
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(caption, GUILayout.Width(150.0f));

            GUIStyle style = new GUIStyle() { fontStyle = FontStyle.Bold };

            if (activeText == null)
            {
                style.normal.textColor = new Color(0.62f, 0, 0);
                EditorGUILayout.LabelField(defaultText, style);
            }
            else
            {
                if (goodText == null || activeText == goodText)
                    style.normal.textColor = new Color(0, 0.62f, 0);
                else
                    style.normal.textColor = new Color(0.2f, 0.2f, 0);

                EditorGUILayout.LabelField(activeText, style);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

        }

        /// <summary>
        /// Parses a string like "2021-01-06T08:52:02.432-08:00" to a UTC DateTime timestamp
        /// </summary>
        /// <param name="str">The string to parse</param>
        /// <returns>the timestamp in UTC</returns>
        public static DateTime ParseTimeString(string str)
        {
            int years = Int32.Parse(str.Substring(0, 4));
            int months = Int32.Parse(str.Substring(5, 2));
            int days = Int32.Parse(str.Substring(8, 2));

            int hours = Int32.Parse(str.Substring(11, 2));
            int minutes = Int32.Parse(str.Substring(14, 2));
            int seconds = Int32.Parse(str.Substring(17, 2));

            int len = str.Length;

            int offs_hours = Int32.Parse(str.Substring(len - 6, 3));
            int offs_minutes = Int32.Parse(str.Substring(len - 2, 2));

            if (offs_hours < 0) offs_minutes = -offs_minutes;

            DateTime t = new DateTime(years, months, days, hours, minutes, seconds);
            // *remove* the timezone offset
            t = t.AddHours(-offs_hours);
            t = t.AddMinutes(-offs_minutes);

            return t;
        }

        private class versionInfo
        {
            public int version = 0;
            public DateTime created = new DateTime();

            public string datestring
            {
                get
                {
                    return created.ToShortDateString() + ", " + created.ToShortTimeString();
                }
            }

            public string versionstring
            {
                get
                {
                    if (version == 0)
                        return null;
                    else if (version != Common.currentUnityVersion)
                        return "outdated (version " + version + ")";
                    else
                        return "current (version " + Common.currentUnityVersion + ")";
                }
            }

            public bool present
            {
                get
                {
                    return version != 0;
                }
            }
        }

        public static void DescribeAssetBundles(List<assetBundleJSON> bundles)
        {

            {
                Dictionary<string, versionInfo> versions = new Dictionary<string, versionInfo>()
                    {
                        { "pc", new versionInfo() },
                        { "android", new versionInfo() },
                        { "mac", new versionInfo() },
                    };

                string currentString = "current (version " + Common.currentUnityVersion + ")";
                foreach (assetBundleJSON ab in bundles)
                {
                    DateTime created = Common.ParseTimeString(ab.created_at);
                    created = created.ToLocalTime();

                    versionInfo v = null;
                    versions.TryGetValue(ab.platform, out v);
                    if (v != null && ab.game_engine_version > v.version)
                        versions[ab.platform] = new versionInfo() { version = ab.game_engine_version, created = created };
                }

                Common.DisplayStatus("  PC", "ABSENT", versions["pc"].versionstring, currentString);
                if (versions["pc"].present)
                    Common.DisplayStatus("", "never", versions["pc"].datestring);

                Common.DisplayStatus("  Android", "ABSENT", versions["android"].versionstring, currentString);
                if (versions["android"].present)
                    Common.DisplayStatus("", "never", versions["android"].datestring);

                Common.DisplayStatus("  Mac", "ABSENT", versions["mac"].versionstring, currentString);
                if (versions["mac"].present)
                    Common.DisplayStatus("", "never", versions["mac"].datestring);
            }
        }

        /// <summary>
        /// OIffers a combined text entry/file selection dialog for the editor GUI
        /// </summary>
        /// <param name="label">Label of item</param>
        /// <param name="folder">Select a folder, not a file</param>
        /// <param name="save">Select to save, rather than to open</param>
        /// <param name="path">Suggested path (absolute or relative to project root)</param>
        /// <param name="extension">when saving a file, the file extension</param>
        /// <returns>The path to the item. Unchanged if the dialog has been canceled.</returns>
        public static string FileSelectionField(GUIContent label, bool folder, bool save, string path, string extension = null)
        {
            EditorGUILayout.BeginHorizontal();

            path = EditorGUILayout.TextField(label, path);

            if (GUILayout.Button("...", GUILayout.Width(20)))
            {
                string newPath = OpenFileDialog(path, folder, save, extension);
                if (newPath != null && newPath != "")
                    path = newPath;
            }

            EditorGUILayout.EndHorizontal();
            return path;
        }

        // The file and directory dialog need some serious boilerplate to actually be useful.
        public static string OpenFileDialog(string path, bool folder, bool save, string extension)
        {
            string newPath;
            if (folder)
                newPath = save
                    ? EditorUtility.SaveFolderPanel("Select directory to save to", path, "")
                    : EditorUtility.OpenFolderPanel("Select directory to open", path, "");
            else
                newPath = save
                    ? EditorUtility.SaveFilePanel("Select file to save to", path, "", extension)
                    : EditorUtility.OpenFilePanel("Select file to open", path, "");

            if (newPath.StartsWith(Application.dataPath))
                newPath = "Assets" + newPath.Substring(Application.dataPath.Length);

            return newPath;
        }

        /// <summary>
        /// Replace interpunction characters in a filename with '_'
        /// </summary>
        /// <param name="filename">Filename or path element name</param>
        /// <returns>Sanitized path element name</returns>
        public static string SanitizeFileName(string filename)
        {
            char[] chars = new char[filename.Length];

            for (int i = 0; i < filename.Length; i++)
            {
                char c = filename[i];
                chars[i] = (!Char.IsLetterOrDigit(c) && c != '_' && c != '.') ? '_' : c;
            }

            return new string(chars);
        }

        public static string GetWebParameter(string content, string pattern)
        {
            string kit_id = null;
            int pos = content.IndexOf(pattern);
            int posEnd = -1;
            if (pos > 0)
            {
                pos += pattern.Length;
                posEnd = content.IndexOf("\"", pos);
            }

            if (posEnd > 0)
                kit_id = content.Substring(pos, posEnd - pos);

            return kit_id;

        }

        /// <summary>
        /// Create a temporary directory with a unique name
        /// </summary>
        /// <returns>Directory path</returns>
        public static string CreateTempDirectory()
        {
            string kitUploadDir;
            do
            {
                kitUploadDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            } while (Directory.Exists(kitUploadDir));

            Directory.CreateDirectory(kitUploadDir);
            return kitUploadDir;
        }

        /// <summary>
        /// Build a AltspaceVR compliant asset Bundle zip out of the given data
        /// </summary>
        /// <param name="assetFiles">Input files (Kit Prefabs or file with preformatted scene)</param>
        /// <param name="screenshotFiles">Kits: Screenshots to kit items</param>
        /// <param name="architectures">Architectures to build for</param>
        /// <param name="tgtRootName">Target root name (must match upload file name of zip file)</param>
        /// <param name="targetFileName">File name to locally save to (incl. .zip) or null to open dialog</param>
        /// <returns>The chosen filename</returns>
        public static string BuildAssetBundle(string[] assetFiles, string[] screenshotFiles, List<BuildTarget> architectures, string tgtRootName, string targetFileName)
        {
            string tmpSaveLocation = Common.CreateTempDirectory();
            string screenshotsSave = Path.Combine(tmpSaveLocation, "Screenshots");

            tgtRootName = SanitizeFileName(tgtRootName).ToLower();

            if (targetFileName == null)
                targetFileName = Common.OpenFileDialog(Path.Combine(Application.dataPath, tgtRootName + ".zip"), false, true, "zip");

            if(string.IsNullOrEmpty(targetFileName))
            {
                Debug.Log("Build has been canceled.");
                return null;
            }

            // Gather screenshots
            if (screenshotFiles.Length > 0)
            {
                if (!Directory.Exists(screenshotsSave))
                    Directory.CreateDirectory(screenshotsSave);

                foreach (string srcFile in screenshotFiles)
                {
                    if (Path.GetExtension(srcFile) != ".png")
                        continue;

                    string srcFileName = Path.GetFileName(srcFile);
                    File.Copy(srcFile, Path.Combine(screenshotsSave, srcFileName));
                }
            }

            AssetBundleBuild[] abb =
            {
                new AssetBundleBuild()
                {
                    assetBundleName = tgtRootName,
                    assetNames = assetFiles
                }
            };

            foreach (BuildTarget architecture in architectures)
            {
                string assetBundlesSave = Path.Combine(tmpSaveLocation, "AssetBundles");
                if (architecture == BuildTarget.Android)
                    assetBundlesSave = Path.Combine(assetBundlesSave, "Android");
                else if (architecture == BuildTarget.StandaloneOSX)
                    assetBundlesSave = Path.Combine(assetBundlesSave, "Mac");

                if (!Directory.Exists(assetBundlesSave))
                    Directory.CreateDirectory(assetBundlesSave);

                AssetBundleManifest am = BuildPipeline.BuildAssetBundles(
                    assetBundlesSave,
                    abb,
                    BuildAssetBundleOptions.StrictMode,
                    architecture);
            }

            using (ZipFile zipFile = new ZipFile())
            {
                zipFile.AddDirectory(tmpSaveLocation);
                zipFile.Save(targetFileName);
            }

            Directory.Delete(tmpSaveLocation, true);
            return targetFileName;
        }

        public static Dictionary<BuildTarget, bool> supported_cache = new Dictionary<BuildTarget, bool>();

        /// <summary>
        /// Checks if build support for the given platform is loaded
        /// </summary>
        /// <param name="target">The target platform</param>
        /// <returns>true if build support is present</returns>
        public static bool IsBuildTargetSupported(BuildTarget target)
        {
            bool res;
            if (!supported_cache.TryGetValue(target, out res))
            {
                Type moduleManager = Type.GetType("UnityEditor.Modules.ModuleManager,UnityEditor.dll");

                MethodInfo getTargetStringFromBuildTarget = moduleManager.GetMethod(
                    "GetTargetStringFromBuildTarget",
                    BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo isPlatformSupportLoaded = moduleManager.GetMethod(
                    "IsPlatformSupportLoaded",
                    BindingFlags.Static | BindingFlags.NonPublic);

                string targetString = (string)getTargetStringFromBuildTarget.Invoke(null, new object[] { target });
                res = (bool)isPlatformSupportLoaded.Invoke(null, new object[] { targetString });

                supported_cache[target] = res;

                if (!res)
                    Debug.LogWarning("Build Support '" + targetString + "' is not installed, building for this platform will be disabled.");
            }

            return res;
        }
    }

}

#endif // UNITY_EDITOR
