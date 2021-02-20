#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
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
}

#endif // UNITY_EDITOR
