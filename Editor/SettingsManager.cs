#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO;
using Unity.XR.Oculus;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace AltSpace_Unity_Uploader
{
    [Serializable]
    public class JSONDictionaryEntry
    {
        public string Key = null;
        public string Value = null;
    }

    [Serializable]
    public class JSONDictionaryList
    {
        public List<JSONDictionaryEntry> jd = new List<JSONDictionaryEntry>();
    }

    [Serializable]
    public class KnownItemsList
    {
        // Json implementation doesn't support dictionary entries, sorry.
        public Dictionary<string, string> known_items = new Dictionary<string, string>();

        // Return true if we enacted a change in the association list
        public bool Update(string _type, string _id, string _itemPath)
        {
            string key = _id + "_" + _type;

            string oldPath;
            if (known_items.TryGetValue(key, out oldPath) && oldPath == _itemPath) return false;

            known_items.Remove(key);
            if(_itemPath != null)
                known_items[key] = _itemPath;

            return true;
        }

        internal string Lookup(string _type, string _id)
        {
            string key = _id + "_" + _type;

            string oldPath;
            return (known_items.TryGetValue(key, out oldPath)) ? oldPath : null;
        }
    }

    [Serializable]
    public class Settings
    {
        public bool RememberLogin = false;
        public bool BuildForPC = true;
        public bool BuildForAndroid = true;
        public bool BuildForMac = true;
        public int SelectShader = 0;
        public bool DefaultShaderOnly = true;
        public bool CheckBuildEnv = true;

        public string KitsRootDirectory = "Assets/Prefabs";
        public bool KitsSetLayer = false;
        public bool KitsSetLightLayer = true;
        public bool KitsNormalizePos = false;
        public bool KitsNormalizeRot = false;
        public bool KitsNormalizeScale = false;
        public bool KitUnsetStatic = true;
        public bool KitsRemoveWhenGenerated = true;
        public bool KitsGenerateScreenshot = true;

        public bool TmplSetLayer = true;
        public bool TmplSetLightLayer = true;
        public bool TmplDeleteCameras = true;
        public bool TmplFixEnviroLight = true;
        public bool TmplSetStatic = false;
    }
    
    public struct LayerInfo
    {
        public LayerInfo(int _layer, string _name)
        {
            layer = _layer;
            name = _name;
        }

        public int layer;
        public string name;
    }

    internal static class ProjectSettingsSetter
    {
        public static void SetProjectSettings()
        {
            SetLayerSettings();
            SetXRPluginsEnabled(true);

            void delayed()
            {
                EditorApplication.update -= delayed;
                SetStereoRenderMode(XRSettings.StereoRenderingMode.SinglePassInstanced);
            };
            EditorApplication.update += delayed;
        }

        private static LayerInfo[] layers =
        {
            // 1 - 8 are predefined by Unity.
            // new LayerInfo(1, "Default" ),
            // new LayerInfo(5, "UI" ),
            new LayerInfo(10, "OwnAvatarLight" ),
            new LayerInfo(14, "NavMesh"),
            new LayerInfo(15, "AvatarLight"),
            new LayerInfo(20, "Hologram"),
            new LayerInfo(25, "MRE Default"),
            new LayerInfo(31, "Interactables")
        };

        /// <summary>
        /// Check and fix the layer settings. 14 for Nav Mesh, 15 for Avatar Lighting.
        /// </summary>
        private static void SetLayerSettings()
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");

            foreach (LayerInfo li in layers)
            {
                SerializedProperty lp = layersProp.GetArrayElementAtIndex(li.layer);
                if (lp != null) lp.stringValue = li.name;
            }

            tagManager.ApplyModifiedProperties();
        }


        private static void SetXRPluginsEnabled(bool enabled)
        {
            XRGeneralSettings SetForBuildTarget(XRGeneralSettingsPerBuildTarget bts, BuildTargetGroup btg, string btgName)
            {
                XRGeneralSettings btSettings = bts.SettingsForBuildTarget(btg);
                if (btSettings == null)
                {
                    btSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
                    btSettings.name = btgName + " Settings";
                    btSettings.Manager = ScriptableObject.CreateInstance<XRManagerSettings>();
                    btSettings.Manager.name = btgName + " Providers";
                    AssetDatabase.AddObjectToAsset(btSettings, bts);
                    AssetDatabase.AddObjectToAsset(btSettings.Manager, bts);
                    bts.SetSettingsForBuildTarget(btg, btSettings);
                }

                return btSettings;
            }

            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget buildTargetSettings))
            {
                if (!AssetDatabase.IsValidFolder("Assets/XR"))
                    AssetDatabase.CreateFolder("Assets", "XR");

                buildTargetSettings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(buildTargetSettings, "Assets/XR/XRGeneralSettings.asset");
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, buildTargetSettings, true);
            }

            XRGeneralSettings standaloneSettings = SetForBuildTarget(buildTargetSettings, BuildTargetGroup.Standalone, "Standalone");
            XRGeneralSettings androidSettings = SetForBuildTarget(buildTargetSettings, BuildTargetGroup.Android, "Android");

            // Enable Oculus XR plugin
            if (enabled)
            {
                XRPackageMetadataStore.AssignLoader(standaloneSettings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Standalone);
                XRPackageMetadataStore.AssignLoader(androidSettings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
            }
            else
            {
                XRPackageMetadataStore.RemoveLoader(standaloneSettings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Standalone);
                XRPackageMetadataStore.RemoveLoader(androidSettings.Manager, "Unity.XR.Oculus.OculusLoader", BuildTargetGroup.Android);
            }

        }

        public static void SetStereoRenderMode(XRSettings.StereoRenderingMode mode)
        {
            var oSettings = AssetDatabase.LoadAssetAtPath<OculusSettings>("Assets/XR/Settings/Oculus Settings.asset");
            if (oSettings == null)
            {
                return;
            }

            switch (mode)
            {
                case XRSettings.StereoRenderingMode.MultiPass:
                    oSettings.m_StereoRenderingModeAndroid = OculusSettings.StereoRenderingModeAndroid.MultiPass;
                    oSettings.m_StereoRenderingModeDesktop = OculusSettings.StereoRenderingModeDesktop.MultiPass;
                    PlayerSettings.stereoRenderingPath = StereoRenderingPath.MultiPass;
                    break;
                case XRSettings.StereoRenderingMode.SinglePassInstanced:
                case XRSettings.StereoRenderingMode.SinglePassMultiview:
                    oSettings.m_StereoRenderingModeAndroid = OculusSettings.StereoRenderingModeAndroid.Multiview;
                    oSettings.m_StereoRenderingModeDesktop = OculusSettings.StereoRenderingModeDesktop.SinglePassInstanced;
                    PlayerSettings.stereoRenderingPath = StereoRenderingPath.Instancing;
                    break;
                case XRSettings.StereoRenderingMode.SinglePass:
                    PlayerSettings.stereoRenderingPath = StereoRenderingPath.SinglePass;
                    break;
                default:
                    Debug.LogErrorFormat("Unsupported render mode: {0}", mode);
                    return;
            }

            EditorUtility.SetDirty(oSettings);
        }
    }

    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class SettingsManager : EditorWindow
    {
        private static string[] m_tabs =
        {
            "General",
            "Kits",
            "Templates"
        };

        private static string[] m_shaders =
        {
            "No change",
            "MRE Diffuse Vertex",
            "MRE Unlit"
        };


        private static string _settingsPath = "Assets/AUU_Settings.json";
        private static string _kilistPath = "Assets/AUU_KnownItems.json";

        private static Settings _settings = null;
        private static KnownItemsList _kilist = null;

        public static bool initialized = false;
        public static string initErrorMsg = "";

        public static bool urppackageinstalled = false;

        public static Settings settings {
            get
            {
                if(_settings == null && File.Exists(_settingsPath))
                {
                    // File.Decrypt(_settingsPath);
                    string json = File.ReadAllText(_settingsPath);
                    _settings = JsonUtility.FromJson<Settings>(json);
                }

                if (_settings == null)
                    _settings = new Settings();

                return _settings;
            }

            set
            {
                _settings = value;
                File.Delete(_settingsPath);
                string text = JsonUtility.ToJson(_settings);
                File.WriteAllText(_settingsPath, text);
                // File.Encrypt(_settingsPath);
            }
        }

        public static KnownItemsList knownItemsList
        {
            get
            {
                if(_kilist == null && File.Exists(_kilistPath))
                {
                    string json = File.ReadAllText(_kilistPath);
                    JSONDictionaryList tmp = JsonUtility.FromJson<JSONDictionaryList>(json);
                    _kilist = new KnownItemsList();
                    foreach(JSONDictionaryEntry entry in tmp.jd)
                        _kilist.known_items.Add(entry.Key, entry.Value);
                }

                if (_kilist == null)
                    _kilist = new KnownItemsList();

                return _kilist;
            }

            set
            {
                _kilist = value;
                File.Delete(_kilistPath);
                JSONDictionaryList tmp = new JSONDictionaryList();
                foreach (KeyValuePair<string, string> entry in _kilist.known_items)
                    tmp.jd.Add(new JSONDictionaryEntry()
                    {
                        Key = entry.Key,
                        Value = entry.Value
                    });

                string text = JsonUtility.ToJson(tmp);
                File.WriteAllText(_kilistPath, text);
            }

        }


        public static void UpdateKnownItem(string _type, string _id, string _itemPath)
        {
            // Only write out if we actually did a change to the list
            if(knownItemsList.Update(_type, _id, _itemPath))
                knownItemsList = _kilist;
        }

        public static string LookupKnownItem(string _type, string _id)
        {
            return knownItemsList.Lookup(_type, _id);
        }

        public static List<BuildTarget> SelectedBuildTargets
        {
            get
            {
                List<BuildTarget> targets = new List<BuildTarget>();
                if (settings.BuildForPC)
                    targets.Add(BuildTarget.StandaloneWindows);

                if (settings.BuildForAndroid)
                    targets.Add(BuildTarget.Android);

                if (settings.BuildForMac)
                    targets.Add(BuildTarget.StandaloneOSX);
                return targets;
            }
        }

        [MenuItem("AUU/Settings", false, 10)]
        public static void ShowSettingsWindow()
        {
            SettingsManager window = GetWindow<SettingsManager>();
            window.Show();
        }

        private int m_selectedTab = 0;

        private static UnityEditor.PackageManager.Requests.AddRequest addResponse;
        private static UnityEditor.PackageManager.Requests.RemoveRequest delResponse;
        private static UnityEditor.PackageManager.Requests.ListRequest listResponse;

        /// <summary>
        /// Check and fix the XR settings. Still using the deprecated XR API.
        /// </summary>
        private static void CheckXRSettings()
        {
            EditorApplication.update -= CheckXRSettings;

            ProjectSettingsSetter.SetProjectSettings();
            Debug.Log("Build settings adapted.");

            EditorApplication.update += CheckURPInstalled;

        }

        private static void PackageListResponse()
        {
            if (!listResponse.IsCompleted) return;

            EditorApplication.update -= PackageListResponse;

            foreach(var package in listResponse.Result)
            {
                if (package.name == "com.unity.render-pipelines.universal")
                    urppackageinstalled = true;
            }

            if(urppackageinstalled)
            {
                // Do not initialize if URP update is in progress and there'd be a reload pending.
                if (!String.IsNullOrEmpty(initErrorMsg = URPInstaller.TriggerStage()))
                    return;
            }

            initialized = true;
        }

        /// <summary>
        /// Does a minimum check whether URP (post-Sep 15th requirements) is installed or not
        /// </summary>
        private static void CheckURPInstalled()
        {
            EditorApplication.update -= CheckURPInstalled;
            listResponse = UnityEditor.PackageManager.Client.List(true);
            EditorApplication.update += PackageListResponse;
        }

        static SettingsManager()
        {
            initialized = false;

            if(Common.usingUnityVersion < Common.minimumUnityVersion)
            {
                Debug.LogError("Your Unity version is too old. You need to update to " + Common.strictUnityVersion + ".");
                initErrorMsg = "Outdated Unity version\ncheck console log";
                return;
            }
            if(Common.usingUnityVersion != Common.currentUnityVersion)
            {
                Debug.LogWarning("Your Unity version is " + Application.unityVersion + ", which is different from a " + Common.relaxedUnityVersion + " version.");
                Debug.LogWarning("It is STRONGLY recommended to install " + Common.strictUnityVersion + " and update this project to use it.");
            }
            else if(Application.unityVersion != Common.strictUnityVersion)
            {
                Debug.LogWarning("Current recommendation is to use Unity " + Common.strictUnityVersion + ".\nOther builds, like your " + Application.unityVersion + " may or may not work.");
            }

            if (settings.CheckBuildEnv)
            {
                Debug.Log("Checking build settings...");
                EditorApplication.update += CheckXRSettings;
            }
            else
                EditorApplication.update += CheckURPInstalled;
        }

        public void OnGUI()
        {
            _ = settings;

            if (!Common.IsBuildTargetSupported(BuildTarget.StandaloneWindows))
                _settings.BuildForPC = false;
            if (!Common.IsBuildTargetSupported(BuildTarget.Android))
                _settings.BuildForAndroid = false;
            if (!Common.IsBuildTargetSupported(BuildTarget.StandaloneOSX))
                _settings.BuildForMac = false;

            if(!initialized)
            {
                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField(new GUIContent("Initializing, please wait..."));
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            m_selectedTab = GUILayout.Toolbar(m_selectedTab, m_tabs);

            EditorGUILayout.Space(20);

            if (m_tabs[m_selectedTab] == "General")
            {
                bool oldRememberLogin = _settings.RememberLogin;

                _settings.RememberLogin = EditorGUILayout.Toggle(new GUIContent(
                    "Remember login state",
                    "Saves the login state between sessions and keeps you logged in."), _settings.RememberLogin);

                // If we just switched it off, delete the saved session data as well.
                if (!_settings.RememberLogin && oldRememberLogin)
                    WebClient.DeleteSessionDataFile();

                // Other way round, if we are logged in and switched this option on, create this file.
                if (_settings.RememberLogin && !oldRememberLogin && WebClient.IsAuthenticated)
                    WebClient.CreateSessionDataFile();

                EditorGUILayout.Space(10);

                if (Common.IsBuildTargetSupported(BuildTarget.StandaloneWindows))
                    _settings.BuildForPC = EditorGUILayout.Toggle(new GUIContent("Build for PC"), _settings.BuildForPC);
                else
                    EditorGUILayout.LabelField(new GUIContent(
                        "Build for PC disabled",
                        "Building for PC is disabled, you need to install the correct module using Unity Hub."));

                if (Common.IsBuildTargetSupported(BuildTarget.Android))
                    _settings.BuildForAndroid = EditorGUILayout.Toggle(new GUIContent("Build for Android"), _settings.BuildForAndroid);
                else
                    EditorGUILayout.LabelField(new GUIContent(
                        "Build for Android disabled",
                        "Building for Android is disabled, you need to install the correct module using Unity Hub."));

                if (Common.IsBuildTargetSupported(BuildTarget.StandaloneOSX))
                    _settings.BuildForMac = EditorGUILayout.Toggle(new GUIContent("Build for macOS"), _settings.BuildForMac);
                else
                    EditorGUILayout.LabelField(new GUIContent(
                        "Build for macOS disabled",
                        "Building for macOS is disabled, you need to install the correct module using Unity Hub."));

                EditorGUILayout.Space(10);

                bool oldCheckBuildEnv = settings.CheckBuildEnv;

                _settings.CheckBuildEnv = EditorGUILayout.Toggle(new GUIContent(
                    "Check Build Environment",
                    "Do some consistency checks and fixes on the build environment, if needed"),
                    _settings.CheckBuildEnv);

                if(!oldCheckBuildEnv && settings.CheckBuildEnv)
                {
                    settings = _settings; // Save settings.
                    Debug.Log("Checking build settings now, hold on tight...");
                    EditorApplication.update += CheckXRSettings;
                }

            }
            else if(m_tabs[m_selectedTab] == "Kits")
            {
                _settings.KitsRootDirectory = Common.FileSelectionField(new GUIContent(
                    "Kits Root Directory",
                    "Root path for all kit data files. Every kit gets its own directory below that one."),
                    true, true, _settings.KitsRootDirectory);

                EditorGUILayout.Space(10);

                _settings.KitsSetLayer = EditorGUILayout.Toggle(new GUIContent(
                    "Set layer to 14",
                    "Set layer of objects to 14, to allow for teleporting"),
                    _settings.KitsSetLayer);

                _settings.KitsSetLightLayer = EditorGUILayout.Toggle(new GUIContent(
                    "Include light layer 15",
                    "Add layer 15 to light culling mask, to allow avatars to be lit as well"),
                    _settings.KitsSetLightLayer);

                EditorGUILayout.Space(10);

                _settings.KitsNormalizePos = EditorGUILayout.Toggle(new GUIContent(
                    "Normalize Position",
                    "Set position to (0,0,0) before exporting"
                    ), _settings.KitsNormalizePos);

                _settings.KitsNormalizeRot = EditorGUILayout.Toggle(new GUIContent(
                    "Normalize Rotation",
                    "Set rotation to (0,0,0) before exporting"
                    ), _settings.KitsNormalizeRot);

                _settings.KitsNormalizeScale = EditorGUILayout.Toggle(new GUIContent(
                    "Normalize Scale",
                    "Set scale to (1,1,1) before exporting"
                    ), _settings.KitsNormalizeScale);

                EditorGUILayout.Space(10);

                _settings.SelectShader = EditorGUILayout.Popup(new GUIContent(
                    "Set shaders to...",
                    "Set the shaders of the kit object to the given one"
                    ), _settings.SelectShader, m_shaders);

                _settings.DefaultShaderOnly = EditorGUILayout.Toggle(new GUIContent(
                    "Default Shader only",
                    "Change only the 'Standard' shader to the given one, leave others unaffected"
                    ), _settings.DefaultShaderOnly);

                _settings.KitUnsetStatic = EditorGUILayout.Toggle(new GUIContent(
                    "Unset 'static' on objects",
                    "Removes the 'static' flag on objects."),
                    _settings.KitUnsetStatic);

                EditorGUILayout.Space(10);

                _settings.KitsRemoveWhenGenerated = EditorGUILayout.Toggle(new GUIContent(
                    "Remove item after generation",
                    "Remove the GameObject from the scene after converting to the kit object"
                    ), _settings.KitsRemoveWhenGenerated);

                _settings.KitsGenerateScreenshot = EditorGUILayout.Toggle(new GUIContent(
                    "Generate Screenshots",
                    "Add Screenshots to the generated items"
                    ), _settings.KitsGenerateScreenshot);
            }
            else if(m_tabs[m_selectedTab] == "Templates")
            {
                _settings.TmplSetLayer = EditorGUILayout.Toggle(new GUIContent(
                    "Set layer to 14",
                    "Set layer of objects to 14, to allow for teleporting"),
                    _settings.TmplSetLayer);

                _settings.TmplSetLightLayer = EditorGUILayout.Toggle(new GUIContent(
                    "Include light layer 15",
                    "Add layer 15 to light culling mask, to allow avatars to be lit as well"),
                    _settings.TmplSetLightLayer);

                _settings.TmplDeleteCameras = EditorGUILayout.Toggle(new GUIContent(
                    "Delete Cameras",
                    "Remove all cameras in the template"),
                    _settings.TmplDeleteCameras);

                _settings.TmplFixEnviroLight = EditorGUILayout.Toggle(new GUIContent(
                    "Fix Environment Lighting",
                    "Set Environment Lighting to 'Gradient' and adapt colors if needed"),
                    _settings.TmplFixEnviroLight);

                _settings.TmplSetStatic = EditorGUILayout.Toggle(new GUIContent(
                    "Set 'static' on objects",
                    "Set the 'static' flags on all objects, making them use baked lighting.\nUSE WITH CAUTION!"),
                    _settings.TmplSetStatic);
            }

            EditorGUILayout.Space(20);

            if (!urppackageinstalled)
            {

                if (GUILayout.Button("Update to URP"))
                {
                    bool result = EditorUtility.DisplayDialog("WARNING!",
                        "!! THERE IS NO TURNING BACK !!\n\n" +
                        "1. Did you create a backup?\n" +
                        "2. Do you know you won't be able to use this project before September 15th in the official, non-BETA client?\n" + 
                        "3. Shader conversion has its limits - custom shaders likely need some editing.\n" +
                        "4. Objects WITHOUT a material (= plain white) need to be assigned the provided URP/EmptyMaterial by hand\n" + 
                        "\n" +
                        "If you're unsure, please do cancel NOW, right here.", "OK", "Cancel");

                    if (result)
                        EditorApplication.update += URPInstaller.BeginUpdate;
                    else
                        EditorUtility.DisplayDialog("Canceled", "Good choice.", "OK");
                }
            }
            else
                EditorGUILayout.LabelField("URP package already installed.");

            EditorGUILayout.Space(20);

            if(GUILayout.Button("Reload Settings"))
            {
                _settings = null;
                _ = settings;
                Repaint();
            }

            if(GUILayout.Button("Save Settings"))
            {
                settings = _settings;
                Close();
            }


            EditorGUILayout.EndVertical();
        }
    }

}

#endif // UNITY_EDITOR
