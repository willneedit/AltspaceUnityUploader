#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{

    public class CreateKitWindow : EditorWindow
    {
        public string kitName = "";
        public string description = "";
        public string imageFile = "";
        public bool rc = false;

        public void OnGUI()
        {

            EditorGUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            kitName = EditorGUILayout.TextField(new GUIContent("Kit Name", "The name of the kit"), kitName);
            EditorGUILayout.LabelField(new GUIContent("Kit Description", "A description for the kit"));
            description = EditorGUILayout.TextArea(description);

            imageFile = Common.FileSelectionField(new GUIContent(
                "Image File", 
                "Optional. An image to be shown in the overview"), 
                false, false, imageFile);

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if(kitName != "")
            {
                if (GUILayout.Button("Create!"))
                {
                    rc = true;
                    Close();
                }
            }

            if(GUILayout.Button("Abort"))
            {
                rc = false;
                Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    public class CreateTemplateWindow : EditorWindow
    {
        public string templateName = "";
        public string description = "";
        public string imageFile = "";
        public string tag_list = "";
        public bool rc = false;

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
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (templateName != "")
            {
                if (GUILayout.Button("Create!"))
                {
                    rc = true;
                    Close();
                }
            }

            if (GUILayout.Button("Abort"))
            {
                rc = false;
                Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }
    }

    public class AltVRItemWidgets
    {
        private static bool m_showWithAssets = false;

        public static void BuildSelectorList<T>(Dictionary<string, T>.ValueCollection vals, Action create_fn, Action load_fn, Action<string> select_fn, ref Vector2 scrollPosition)
    where T : AltspaceListItem, new()
        {
            string item_type = null;

            GUILayout.BeginVertical(new GUIStyle { padding = new RectOffset(10, 10, 10, 10) });

            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal(GUILayout.Width(240.0f));

            m_showWithAssets = EditorGUILayout.Toggle(m_showWithAssets);
            EditorGUILayout.LabelField("Show only items with local assets");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            bool shownOne = false;

            {
                GUIStyle style = new GUIStyle() { fontStyle = FontStyle.Bold };

                // We got at least one item, pick the type from one.
                if(vals.Count > 0)
                    item_type = vals.First().friendlyName;

                scrollPosition = GUILayout.BeginScrollView(scrollPosition);

                EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));
                EditorGUILayout.LabelField(" ", style);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Local", style, GUILayout.Width(60.0f));
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));
                EditorGUILayout.LabelField("Name", style);
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Asset", style, GUILayout.Width(60.0f));
                EditorGUILayout.EndHorizontal();

                foreach (var item in vals)
                {
                    if (m_showWithAssets && !item.isSet)
                        continue;

                    EditorGUILayout.BeginHorizontal(GUILayout.Width(120.0f));

                    EditorGUILayout.LabelField(item.itemName);
                    GUILayout.FlexibleSpace();

                    if (item.exists)
                    {
                        style.normal.textColor = new Color(0, 0.62f, 0);
                        EditorGUILayout.LabelField("OK", style, GUILayout.Width(60.0f));
                    }
                    else if(item.isSet)
                    {
                        style.normal.textColor = new Color(0.62f, 0, 0);
                        EditorGUILayout.LabelField("missing", style, GUILayout.Width(60.0f));
                    }
                    else
                        EditorGUILayout.LabelField(" ", style, GUILayout.Width(60.0f));

                    if (GUILayout.Button("Select", EditorStyles.miniButton))
                        select_fn(item.id);

                    EditorGUILayout.EndHorizontal();

                    shownOne = true;
                }

                GUILayout.EndScrollView();
            }

            if(!shownOne)
            {
                // We had no item to read the type from, create an empty "blueprint item" to infer it.
                T blp_item = new T();
                item_type = blp_item.friendlyName;

                if (m_showWithAssets && vals.Count > 0)
                    GUILayout.Label(
                        "All " + item_type + "s are filtered out, you might want" +
                        "to deselect 'Show only items with local assets'"
                        , new GUIStyle() { fontStyle = FontStyle.Bold });
                else
                    GUILayout.Label(
                        "No " + item_type + "s loaded. Either press \"Load " + item_type + "s\"\n" +
                        "to load known " + item_type + "s from the account,\n" +
                        "Or press \"Create new " + item_type + "\" to create a new one."
                        , new GUIStyle() { fontStyle = FontStyle.Bold });
            }

            if (GUILayout.Button("Load " + item_type + "s"))
                load_fn();

            if (GUILayout.Button("Create new " + item_type))
                create_fn();

            GUILayout.EndVertical();
        }

        public static void ManageItem(AltspaceListItem item, Action showSelection_fn, Action<string> updateItem_fn, string missingString)
        {
            void BuildItem(AltspaceListItem subItem)
            {
                string state = subItem.buildAssetBundle(SettingsManager.SelectedBuildTargets) ? "finished" : "canceled";
                LoginManager window = EditorWindow.GetWindow<LoginManager>();
                window.ShowNotification(new GUIContent(subItem.friendlyName.Capitalize() + " build " + state), 5.0f);
            }

            void BuildAndUploadItem(AltspaceListItem subItem, Action<string> updategui_fn)
            {
                LoginManager.BuildAndUploadAltVRItem(SettingsManager.SelectedBuildTargets, subItem);

                updategui_fn(subItem.id);

                LoginManager window = EditorWindow.GetWindow<LoginManager>();
                window.ShowNotification(new GUIContent(subItem.friendlyName.Capitalize() + " upload finished"), 5.0f);

            }

            if (WebClient.IsAuthenticated)
            {
                if (GUILayout.Button("Select " + item.friendlyName.Capitalize()))
                    showSelection_fn();
            }
            else
                EditorGUILayout.LabelField("Offline mode", new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });

            EditorGUILayout.Space(10);

            item.showSelf();

            EditorGUILayout.BeginHorizontal();

            if (!item.isSet)
                GUILayout.Label(missingString, new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if (item.exists)
            {
                if (GUILayout.Button("Build"))
                    EditorApplication.delayCall += () => BuildItem(item);

                if (item.isSelected)
                {
                    if (GUILayout.Button("Build & Upload"))
                        EditorApplication.delayCall += () => BuildAndUploadItem(item, updateItem_fn);
                }

            }

            EditorGUILayout.EndHorizontal();
        }

    }
}

#endif // UNITY_EDITOR
