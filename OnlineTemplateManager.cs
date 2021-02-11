#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class OnlineTemplateManager : EditorWindow
    {
        public class templateInfo
        {
            public string templateSceneName = null;
            public templateJSON template_data = new templateJSON();
        }

        private static Dictionary<string, templateInfo> _known_templates = new Dictionary<string, templateInfo>();
        private static templateInfo _selected_template = new templateInfo() { templateSceneName = "" };
        
        public static bool HasLoadedTemplates { get { return _known_templates.Count > 0; } }
        public static bool HasTemplateSceneSelected { get { return !string.IsNullOrEmpty(_selected_template.templateSceneName); } }
        public static bool HasTemplateSelected { get { return !string.IsNullOrEmpty(_selected_template.template_data.space_template_id); } }

        public static string templateScene { get { return _selected_template.templateSceneName; } }
        public static string sceneAssetName { get { return Path.Combine("Assets", "Scenes", templateScene + ".unity"); } }


        public static void ShowSelectedTemplate()
        {
            templateJSON td = _selected_template.template_data;
            if(LoginManager.IsConnected)
            {
                EditorGUILayout.LabelField("Selected Template:");
                Common.DisplayStatus("  Name:", "none", td.activity_name);
                Common.DisplayStatus("  ID:", "none", td.space_template_id);
            }

            if(HasTemplateSelected)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Template contents:");

                // TODO: Any reason where there are more than one asset bundle scenes? 
                Common.DescribeAssetBundles(td.asset_bundle_scenes[0].asset_bundles);
            }

            EditorGUILayout.BeginHorizontal();

            _selected_template.templateSceneName = EditorGUILayout.TextField(_selected_template.templateSceneName);
            if (GUILayout.Button("Use current scene"))
                _selected_template.templateSceneName = EditorSceneManager.GetActiveScene().name;

            EditorGUILayout.EndHorizontal();
        }

        public static void ResetContents()
        {
            OnlineTemplateManager window = GetWindow<OnlineTemplateManager>();
            window.Close();
            _known_templates = new Dictionary<string, templateInfo>();
            _selected_template = new templateInfo();
        }

        private static string GetSuggestedSceneName(string template_id, string template_name)
        {
            return template_id + "_" + Common.SanitizeFileName(template_name).ToLower();
        }

        private string CreateTemplate(string name, string description, string imageFileName, string tag_list)
        {
            string result = LoginManager.CreateAltVRItem(
                "space_template",
                name,
                description,
                imageFileName,
                tag_list);
            ShowNotification(new GUIContent(
                "Template registration " + ((result != null)
                ? "successful"
                : "failed")));
            return result;
        }

        private static void EnterTemplateData(templateJSON tmpl)
        {
            if (tmpl.asset_bundle_scenes.Count > 0 && tmpl.asset_bundle_scenes[0].user_id != LoginManager.userid) return;

            _known_templates.Remove(tmpl.space_template_id);
            _known_templates.Add(tmpl.space_template_id, new templateInfo()
            {
                templateSceneName = GetSuggestedSceneName(tmpl.space_template_id, tmpl.activity_name),
                template_data = tmpl
            });
        }

        private static bool LoadSingleTemplate(string template_id)
        {
            templateJSON tmpl = LoginManager.LoadSingleAltVRItem<templateJSON>(template_id);
            if(tmpl != null && !string.IsNullOrEmpty(tmpl.activity_name))
            {
                EnterTemplateData(tmpl);
                return true;
            }
            return false;
        }

        private void LoadTemplates()
        {
            LoginManager.LoadAltVRItems((templatesJSON content) =>
            {
                foreach (templateJSON tmpl in content.space_templates)
                    EnterTemplateData(tmpl);
            });

            if (_known_templates.Count == 0)
                ShowNotification(new GUIContent("No own kits"), 5.0f);

        }

        public static void ManageTemplates()
        {
            if (LoginManager.IsConnected)
            {
                if (GUILayout.Button("Select Template"))
                    ShowTemplateSelection();
            }
            else
                EditorGUILayout.LabelField("Offline mode", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

            EditorGUILayout.Space(10);

            ShowSelectedTemplate();

            string template_id = _selected_template.template_data.space_template_id;

            bool existsTemplateScene = HasTemplateSceneSelected && (EditorSceneManager.GetSceneByName(templateScene).IsValid());
            bool isStandardTemplateScene =
                HasTemplateSceneSelected
                && (string.IsNullOrEmpty(template_id) ||
                    GetSuggestedSceneName(template_id, _selected_template.template_data.activity_name) == templateScene);

            if(HasTemplateSceneSelected && !isStandardTemplateScene)
            {
                GUILayout.Label("The scene name doesn't match the standard format.\nRenaming the scene is recommended.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

                {
                    string new_templateScene = GetSuggestedSceneName(template_id, _selected_template.template_data.activity_name);
                    string new_sceneAssetName = Path.Combine("Assets", "Scenes", new_templateScene + ".unity");

                    if (File.Exists(new_sceneAssetName))
                        GUILayout.Label("Yet there is already a scene with the suggested name.\nYou may want to use that one.", new GUIStyle()
                        {
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        });
                    else if (EditorSceneManager.GetActiveScene().isDirty)
                        GUILayout.Label("But you should save the current scene first.", new GUIStyle()
                        {
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        });
                    else if (GUILayout.Button("Rename Scene"))
                    {
                        if (existsTemplateScene)
                        {
                            EditorSceneManager.OpenScene(sceneAssetName);
                            Scene sc = EditorSceneManager.GetSceneByName(templateScene);
                            if (sc.IsValid())
                            {
                                EditorSceneManager.SaveScene(sc, new_sceneAssetName);
                                File.Delete(sceneAssetName + ".meta");
                                File.Delete(sceneAssetName);
                            }
                        }

                        _selected_template.templateSceneName = new_templateScene;
                        AssetDatabase.Refresh();
                    }

                }
            }
            if(HasTemplateSceneSelected && !existsTemplateScene)
            {
                GUILayout.Label("The scene isn't loaded.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

                if (File.Exists(sceneAssetName))
                    GUILayout.Label("Yet, there is a scene saved under the given name\nPlease load it into the editor.", new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });
                else
                {
                    GUILayout.Label("Press the button to save the current scene under this name.", new GUIStyle()
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    });

                    if (GUILayout.Button("Save current scene"))
                    {
                        Scene sc = EditorSceneManager.GetActiveScene();
                        EditorSceneManager.SaveScene(sc, sceneAssetName);
                    }
                }

            }

            EditorGUILayout.BeginHorizontal();

            if (!HasTemplateSceneSelected)
                GUILayout.Label("You need to set the scene name\nbefore you can build templates.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if(existsTemplateScene)
            {
                if (GUILayout.Button("Build"))
                    EditorApplication.update += BuildTemplate;

                if (HasTemplateSelected)
                {
                    if (GUILayout.Button("Build & Upload"))
                        EditorApplication.update += BuildAndUploadTemplate;
                }

            }

            EditorGUILayout.EndHorizontal();
        }

        private static void BuildTemplate()
        {
            EditorApplication.update -= BuildTemplate;
            string state = TemplateBuilder.BuildTemplateAssetBundle(SettingsManager.SelectedBuildTargets) ? "finished" : "canceled";
            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Template build " + state), 5.0f);
        }

        private static void BuildAndUploadTemplate()
        {
            EditorApplication.update -= BuildAndUploadTemplate;

            List<BuildTarget> targets = SettingsManager.SelectedBuildTargets;
            string item_type_singular = "space_template";
            string itemRootName = templateScene.ToLower();
            string item_id = _selected_template.template_data.space_template_id;

            LoginManager.BuildAndUploadAltVRItem(targets, item_type_singular, itemRootName, item_id);

            // Reload kit data (and update display)
            LoadSingleTemplate(item_id);
            _selected_template = _known_templates[item_id];

            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Template upload finished"), 5.0f);

        }


        public static void ShowTemplateSelection()
        {
            OnlineTemplateManager window = GetWindow<OnlineTemplateManager>();
            window.Show();
        }

        private Vector2 m_scrollPosition;

        public void OnGUI()
        {
            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            if (HasLoadedTemplates)
            {
                m_scrollPosition = GUILayout.BeginScrollView(m_scrollPosition);
                foreach (var tmpl in _known_templates)
                {
                    EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));

                    EditorGUILayout.LabelField(tmpl.Value.template_data.activity_name);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                    {
                        _selected_template = _known_templates[tmpl.Value.template_data.space_template_id];
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
                    "No templates loaded.\n" +
                    "Either press \"Load templates\"\n" +
                    "to load known templates from the account,\n" +
                    "Or press \"Create New Template\"\n" +
                    "to create a new one.", new GUIStyle() { fontStyle = FontStyle.Bold });
            }

            if (GUILayout.Button("Load Templates"))
                LoadTemplates();

            if (GUILayout.Button("Create New Template"))
            {
                CreateTemplateWindow window = CreateInstance<CreateTemplateWindow>();
                window.ShowModalUtility();
                if (window.rc)
                {
                    string tmpl_id = CreateTemplate(window.templateName, window.description, window.imageFile, window.tag_list);
                    if (LoadSingleTemplate(tmpl_id))
                        _selected_template = _known_templates[tmpl_id];
                }
            }
            // CreateKit("__AUUTest", "This is a test for the AUU kit creation", "D:/Users/carsten/Pictures/Sweet-Fullscene.png");
            GUILayout.EndVertical();
        }


    }
}

#endif // UNITY_EDITOR