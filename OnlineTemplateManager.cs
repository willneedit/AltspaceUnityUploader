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
        private static Dictionary<string, AltspaceTemplateItem> _known_templates = new Dictionary<string, AltspaceTemplateItem>();
        private static AltspaceTemplateItem _selected_template = new AltspaceTemplateItem();
        
        public static bool HasLoadedTemplates { get { return _known_templates.Count > 0; } }

        public static void ShowSelectedTemplate()
        {
            if(LoginManager.IsConnected)
            {
                EditorGUILayout.LabelField("Selected Template:");
                Common.DisplayStatus("  Name:", "none", _selected_template.itemName);
                Common.DisplayStatus("  ID:", "none", _selected_template.id);
            }

            if(_selected_template.isSelected)
            {
                EditorGUILayout.Space(10);

                EditorGUILayout.LabelField("Template contents:");

                // TODO: Any reason where there are more than one asset bundle scenes? 
                Common.DescribeAssetBundles(_selected_template.asset_bundles);
            }

            EditorGUILayout.BeginHorizontal();

            _selected_template.itemPath = EditorGUILayout.TextField(_selected_template.itemPath);
            if (GUILayout.Button("Use current scene"))
                _selected_template.chooseAssetPath();

            EditorGUILayout.EndHorizontal();
        }

        public static void ResetContents()
        {
            OnlineTemplateManager window = GetWindow<OnlineTemplateManager>();
            window.Close();
            _known_templates = new Dictionary<string, AltspaceTemplateItem>();
            _selected_template = new AltspaceTemplateItem();
        }

        private static void EnterTemplateData(templateJSON tmpl)
        {
            if (tmpl.asset_bundle_scenes.Count > 0 && tmpl.asset_bundle_scenes[0].user_id != LoginManager.userid) return;

            _known_templates.Remove(tmpl.space_template_id);
            AltspaceTemplateItem new_item = new AltspaceTemplateItem();
            new_item.importAltVRItem(tmpl);
            _known_templates.Add(tmpl.space_template_id, new_item);
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

            string template_id = _selected_template.id;


            if(_selected_template.isSet && !EditorSceneManager.GetSceneByName(_selected_template.templateSceneName).IsValid())
            {
                GUILayout.Label("The scene isn't loaded.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

                if (_selected_template.exists)
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
                        EditorSceneManager.SaveScene(sc, _selected_template.itemPath);
                    }
                }

            }

            EditorGUILayout.BeginHorizontal();

            if (!_selected_template.isSet)
                GUILayout.Label("You need to set the scene name\nbefore you can build templates.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if(_selected_template.exists)
            {
                if (GUILayout.Button("Build"))
                    EditorApplication.update += BuildTemplate;

                if (_selected_template.isSelected)
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
            string state = _selected_template.buildAssetBundle(SettingsManager.SelectedBuildTargets) ? "finished" : "canceled";
            LoginManager window = GetWindow<LoginManager>();
            window.ShowNotification(new GUIContent("Template build " + state), 5.0f);
        }

        private static void BuildAndUploadTemplate()
        {
            EditorApplication.update -= BuildAndUploadTemplate;

            string item_id = _selected_template.id;

            LoginManager.BuildAndUploadAltVRItem(SettingsManager.SelectedBuildTargets, _selected_template);

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

                    EditorGUILayout.LabelField(tmpl.Value.itemName);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                    {
                        _selected_template = _known_templates[tmpl.Value.id];
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
                    AltspaceTemplateItem new_item = new AltspaceTemplateItem()
                    {
                        itemName = window.templateName,
                        description = window.description,
                        imageFile = window.imageFile,
                        tag_list = window.tag_list
                    };

                    if (new_item.updateAltVRItem() && LoadSingleTemplate(new_item.id))
                    {
                        _selected_template = _known_templates[new_item.id];
                        _selected_template.itemPath = Path.Combine(
                            "Assets",
                            "Scenes",
                            Common.SanitizeFileName(_selected_template.itemName) + ".unity");

                        this.Close();
                        GetWindow<LoginManager>().Repaint();
                    }
                }
            }
            GUILayout.EndVertical();
        }


    }
}

#endif // UNITY_EDITOR