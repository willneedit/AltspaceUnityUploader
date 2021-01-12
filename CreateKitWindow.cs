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
}

#endif // UNITY_EDITOR
