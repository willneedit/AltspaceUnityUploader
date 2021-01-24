#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class TemplateBuilder : MonoBehaviour
    {

        public static void RelayerTree(GameObject go)
        {
            go.layer = 14;
            for (int i = 0; i < go.transform.childCount; ++i)
                RelayerTree(go.transform.GetChild(i).gameObject);
        }

        public static void SetStatic(GameObject go)
        {
            go.isStatic = true;
            for (int i = 0; i < go.transform.childCount; ++i)
                SetStatic(go.transform.GetChild(i).gameObject);
        }

        public static string BuildTemplateScene()
        {
            Scene sc = EditorSceneManager.GetActiveScene();

            // Should never happen, buttons should not be shown
            if (sc.name != OnlineTemplateManager.templateScene) return null;

            // Save first
            if (sc.isDirty)
                EditorSceneManager.SaveScene(sc);


            string tmpSceneName = Path.Combine("Assets", "Scenes", "_" + Path.GetRandomFileName() + ".unity");

            // Save in a temporary scene, using a different file name
            EditorSceneManager.SaveScene(sc, tmpSceneName);

            GameObject env = GameObject.Find("Environment");

            if (env == null)
                env = new GameObject("Environment");

            List<GameObject> looseObjects = sc.GetRootGameObjects().ToList().FindAll(x =>
                x.name != "Environment"
            );

            // Reparent all the loose objects below "Environment"
            foreach (GameObject l in looseObjects) l.transform.SetParent(env.transform);

            // Relayer "Environment" and everything below (which is everything else) to layer 14, if so wanted
            if (SettingsManager.settings.TmplSetLayer)
                RelayerTree(env);

            // Set everything to 'static', if so wanted
            if (SettingsManager.settings.TmplSetStatic)
                SetStatic(env);

            // Delete all cameras, if so wanted
            if(SettingsManager.settings.TmplDeleteCameras)
            {
                foreach (Camera camera in FindObjectsOfType<Camera>())
                    DestroyImmediate(camera.gameObject);
            }

            // Add lights to layer 15, if so wanted
            if(SettingsManager.settings.TmplSetLightLayer)
            {
                foreach(Light light in env.GetComponentsInChildren<Light>())
                    light.cullingMask |= 1 << 15;
            }

            if(SettingsManager.settings.TmplFixEnviroLight)
            {
                if(RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Trilight)
                {
                    // If it's flat lighting, derive the Trilight from the single one
                    if(RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
                    {
                        RenderSettings.ambientSkyColor = RenderSettings.ambientLight;
                        RenderSettings.ambientEquatorColor = RenderSettings.ambientSkyColor;
                        RenderSettings.ambientGroundColor = RenderSettings.ambientSkyColor;
                    }
                    else
                    {
                        RenderSettings.ambientSkyColor = Color.gray;
                        RenderSettings.ambientEquatorColor = Color.gray;
                        RenderSettings.ambientGroundColor = Color.gray;
                        Debug.LogWarning("Cannot determine Environment Lighting color - please check, and use 'Gradient' to set\nUsing Gray as default.");
                    }

                    RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                }
            }

            bool noDirectional = true;
            foreach(Light light in env.GetComponentsInChildren<Light>())
            {
                if (light.type == LightType.Directional && light.lightmapBakeType == LightmapBakeType.Realtime)
                    noDirectional = false;
            }

            if (noDirectional)
                Debug.LogWarning("There's no Realtime directional light. Altspace will add a default white light.");

            EditorSceneManager.SaveScene(sc);

            return tmpSceneName;
        }

        public static void BuildTemplateAssetBundle(List<BuildTarget> architectures, string targetFileName = null)
        {
            string tmpSceneAssetName = BuildTemplateScene();

            string[] assetFiles = new string[] {
                tmpSceneAssetName
            };
            string[] screenshotFiles = new string[0];
            string tgtRootName = OnlineTemplateManager.templateScene;


            targetFileName = Common.BuildAssetBundle(assetFiles, screenshotFiles, architectures, tgtRootName, targetFileName);

            EditorSceneManager.OpenScene(OnlineTemplateManager.sceneAssetName);
            File.Delete(tmpSceneAssetName + ".meta");
            File.Delete(tmpSceneAssetName);
            AssetDatabase.Refresh();
        }
    }

}

#endif // UNITY_EDITOR