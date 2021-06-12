#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
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

        public static void ShowTemplate(AltspaceTemplateItem tmpl)
        {
            Common.ShowItem(tmpl);

            EditorGUILayout.BeginHorizontal();

            tmpl.itemPath = EditorGUILayout.TextField(tmpl.itemPath);
            if (GUILayout.Button("Use current scene"))
                tmpl.chooseAssetPath();

            EditorGUILayout.EndHorizontal();

            if (tmpl.isSet && !EditorSceneManager.GetSceneByName(tmpl.templateSceneName).IsValid())
            {
                GUILayout.Label("The scene isn't loaded.", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

                if (tmpl.exists)
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
                        EditorSceneManager.SaveScene(sc, tmpl.itemPath);
                    }
                }

            }
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

        public static void ManageTemplates()
        {
            AltVRItemWidgets.ManageItem(
                _selected_template,
                () => GetWindow<OnlineTemplateManager>().Show(),
                (string id) => { LoadSingleTemplate(id); _selected_template = _known_templates[id]; },
                "You need to set the scene name\nbefore you can build templates.");
        }


        private Vector2 m_scrollPosition;

        public void OnGUI()
        {
            AltVRItemWidgets.BuildSelectorList(_known_templates.Values, CreateTemplate, LoadTemplates, SelectTemplate, ref m_scrollPosition);

            void SelectTemplate(string id)
            {
                _selected_template = _known_templates[id];
                this.Close();
                GetWindow<LoginManager>().Repaint();
            }

            void CreateTemplate()
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

            void LoadTemplates()
            {
                LoginManager.LoadAltVRItems((templatesJSON content) =>
                {
                    foreach (templateJSON tmpl in content.space_templates)
                        EnterTemplateData(tmpl);
                });

                if (_known_templates.Count == 0)
                    ShowNotification(new GUIContent("No own templates"), 5.0f);

            }

        }


    }
}

#endif // UNITY_EDITOR