#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [Serializable]
    public class Settings
    {
        public string Login = "";
        public string Password = "";
        public bool BuildForPC = true;
        public bool BuildForAndroid = true;
        public bool BuildForMac = true;
        public bool CheckBuildEnv = true;

        public string KitsRootDirectory = "Assets/Prefabs";
        public bool KitsSetLayer = false;
        public bool KitsSetLightLayer = true;
        public bool KitsNormalizePos = false;
        public bool KitsNormalizeRot = false;
        public bool KitsNormalizeScale = false;
        public bool KitsRemoveWhenGenerated = true;
        public bool KitsGenerateScreenshot = true;
        public int KitsSelectShader = 0;

    }

    [ExecuteInEditMode]
    public class SettingsManager : EditorWindow
    {
        private static string _settingsPath = "Assets/AUU_Settings.json";

        private static Settings _settings = null;
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

        [MenuItem("AUU/Settings", false, 10)]
        public static void ShowSettingsWindow()
        {
            SettingsManager window = GetWindow<SettingsManager>();
            window.Show();
        }

        private int m_selectedTab = 0;

        private string[] m_tabs =
        {
            "General",
            "Kits",
            "Templates"
        };

        private string[] m_shaders =
        {
            "No change",
            "MRE Diffuse Vertex",
            "MRE Unlit"
        };

        public void OnGUI()
        {
            _ = settings;

            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            m_selectedTab = GUILayout.Toolbar(m_selectedTab, m_tabs);

            EditorGUILayout.Space(20);

            if (m_tabs[m_selectedTab] == "General")
            {
                _settings.Login = EditorGUILayout.TextField(new GUIContent("EMail", "The EMail you've registered yourself to Altspace with."), _settings.Login);
                _settings.Password = EditorGUILayout.PasswordField(new GUIContent("Password", "Your password"), _settings.Password);

                EditorGUILayout.Space(10);

                _settings.BuildForPC = EditorGUILayout.Toggle(new GUIContent("Build for PC"), _settings.BuildForPC);
                _settings.BuildForAndroid = EditorGUILayout.Toggle(new GUIContent("Build for Android"), _settings.BuildForAndroid);
                _settings.BuildForMac = EditorGUILayout.Toggle(new GUIContent("Build for macOS"), _settings.BuildForMac);

                EditorGUILayout.Space(10);

                _settings.CheckBuildEnv = EditorGUILayout.Toggle(new GUIContent(
                    "Check Build Environment",
                    "Do some consistency checks and fixes on the build environment, if needed"),
                    _settings.CheckBuildEnv);
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

                _settings.KitsSelectShader = EditorGUILayout.Popup(new GUIContent(
                    "Set shaders to...",
                    "Set the shaders of the kit object to the given one"
                    ) ,_settings.KitsSelectShader, m_shaders);

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

            }



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
