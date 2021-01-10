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

        public void OnGUI()
        {
            _ = settings;

            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

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

            EditorGUILayout.Space(10);

            _settings.KitsRootDirectory = Common.FileSelectionField(new GUIContent(
                "Kits Root Directory",
                "Root path for all kit data files. Every kit gets its own directory below that one."),
                true, true, _settings.KitsRootDirectory);

            //_settings.KitsRootDirectory = EditorGUILayout.TextField(new GUIContent(
            //    "Kits Root Directory",
            //    "Root path for all kit data files. Every kit gets its own directory below that one."),
            //    _settings.KitsRootDirectory);

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

