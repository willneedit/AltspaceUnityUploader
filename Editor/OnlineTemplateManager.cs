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
    public class OnlineTemplateManager : OnlineManagerBase<AltspaceTemplateItem, templateJSON>
    {
        private class CreateTemplateWindow : CreateWindowBase
        {
            public string templateName = "";
            public string description = "";
            public string imageFile = "";
            public string tag_list = "";

            public void OnGUI()
            {

                EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

                templateName = EditorGUILayout.TextField(new GUIContent(
                    "Template Name",
                    "The name of the template"
                    ), templateName);
                EditorGUILayout.LabelField(new GUIContent("Template Description", "A description for the template"));
                description = EditorGUILayout.TextArea(description);

                imageFile = Common.FileSelectionField(new GUIContent(
                    "Image File",
                    "Optional. An image to be shown in the overview"),
                    false, false, imageFile);

                tag_list = EditorGUILayout.TextField(new GUIContent(
                    "Tag List",
                    "Comma separated list of tags you'd wish to enter for your template."
                    ), tag_list);
                Trailer(() => templateName != "");
            }
        }

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

        public static void ManageTemplates()
        {
            ManageItems<OnlineTemplateManager>("You need to set the scene name\nbefore you can build templates.");
        }


        private Vector2 m_scrollPosition;

        public void OnGUI()
        {
            AltVRItemWidgets.BuildSelectorList(_known_items.Values, CreateTemplate, LoadTemplates, SelectItem, ref m_scrollPosition);

            void CreateTemplate()
            {
                CreateTemplateWindow window = CreateInstance<CreateTemplateWindow>();
                window.SetCommitAction(() =>
                    {
                        AltspaceTemplateItem new_item = new AltspaceTemplateItem()
                        {
                            itemName = window.templateName,
                            description = window.description,
                            imageFile = window.imageFile,
                            tag_list = window.tag_list
                        };

                        if (new_item.updateAltVRItem() && LoadSingleItem(new_item.id))
                        {
                            _selected_item = _known_items[new_item.id];
                            _selected_item.itemPath = Path.Combine(
                                "Assets",
                                "Scenes",
                                Common.SanitizeFileName(_selected_item.itemName) + ".unity");

                            this.Close();
                            GetWindow<LoginManager>().Repaint();
                        }
                    });
                window.Show();
            }

            void LoadTemplates()
            {
                LoginManager.LoadAltVRItems((templatesJSON content) =>
                {
                    foreach (templateJSON tmpl in content.space_templates)
                        if (tmpl.asset_bundle_scenes.Count > 0 && tmpl.asset_bundle_scenes[0].user_id == LoginManager.userid)
                            EnterItemData(tmpl);
                });

                if (_known_items.Count == 0)
                    ShowNotification(new GUIContent("No own templates"), 5.0f);

            }

        }


    }
}

#endif // UNITY_EDITOR