#if UNITY_EDITOR

using Ionic.Zip;
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
    public class KitBuilder : MonoBehaviour
    {
        private string rootDir;

        private static void CreateScreenshot(GameObject prefab)
        {
            if (!SettingsManager.settings.KitsGenerateScreenshot) return;

            string shotsPath = Path.Combine(OnlineKitManager.kitRoot, "Screenshots");

            Texture2D assetPreview = null;
            for(int tmo = 0;tmo < 50; ++tmo)
            {
                if ((assetPreview = AssetPreview.GetAssetPreview(prefab)) != null) break;
                System.Threading.Thread.Sleep(6);
            }

            // No preview within five minutes, something has to be wrong.
            if (assetPreview == null) return;

            // Turn all background pixels to transparent.
            Color blankPixel = assetPreview.GetPixel(0, 0);
            for(int x = 0;x < assetPreview.width; ++x)
                for(int y = 0;y < assetPreview.height; ++y)
                    if (assetPreview.GetPixel(x, y) == blankPixel)
                        assetPreview.SetPixel(x, y, Color.clear);

            assetPreview.alphaIsTransparency = true;
            assetPreview.Apply();

            if (!Directory.Exists(shotsPath))
                Directory.CreateDirectory(shotsPath);

            byte[] bytes = assetPreview.EncodeToPNG();

            string pngPath = Path.Combine(shotsPath, prefab.name + ".png");
            File.WriteAllBytes(pngPath, bytes);
        }

        private static void RedoLights(GameObject go)
        {
            if (!SettingsManager.settings.KitsSetLightLayer) return;

            Light[] ls = go.GetComponentsInChildren<Light>();

            foreach(Light l in ls)
            {
                l.lightmapBakeType = LightmapBakeType.Realtime;
                l.cullingMask |= (1 << 15) | (1 << 10);
            }
        }

        public static void UnsetStatic(GameObject go)
        {
            go.isStatic = false;
            for (int i = 0; i < go.transform.childCount; ++i)
                UnsetStatic(go.transform.GetChild(i).gameObject);
        }

        private static void RedoShaders(GameObject go)
        {
            int shaderSel = SettingsManager.settings.SelectShader;

            Shader sh = null;

            if (shaderSel == 1)
                sh = Shader.Find("MRE/DiffuseVertex");
            else if (shaderSel == 2)
                sh = Shader.Find("MRE/Unlit (Supports Lightmap)");

            // Either 'no change' or shader not found.
            if (sh == null) return;

            Renderer[] rs = go.GetComponentsInChildren<Renderer>();

            Shader standard = Shader.Find("Standard");

            foreach (Renderer r in rs)
            {
                if(r.sharedMaterial.shader == standard || !SettingsManager.settings.DefaultShaderOnly)
                    r.sharedMaterial.shader = sh;
            }
        }

        private static void NormalizeGO(GameObject go)
        {
            Settings s = SettingsManager.settings;

            if (s.KitsNormalizePos)
                go.transform.localPosition = new Vector3(0, 0, 0);

            if (s.KitsNormalizeRot)
                go.transform.localEulerAngles = new Vector3(0, 0, 0);

            if (s.KitsNormalizeScale)
                go.transform.localScale = new Vector3(1, 1, 1);
        }


        /// <summary>
        /// Copies the collider component onto target, retaining its properties and transform in world space
        /// </summary>
        /// <param name="source">the collider component to copy</param>
        /// <param name="target">the target hierarchy</param>
        /// <param name="useSubObject">place the collider on a sub object, rather than target itself</param>
        /// <returns>true if collider has been added</returns>
        private static bool CopyCollider(Collider source, GameObject target, bool useSubObject)
        {
            if(useSubObject)
            {
                GameObject retarget = new GameObject("subCollider");
                retarget.transform.SetParent(target.transform);
                target = retarget;
            }

            if (source.GetType() == typeof(MeshCollider))
            {
                MeshCollider mc = (MeshCollider)source;

                MeshCollider copy = target.AddComponent<MeshCollider>();
                copy.enabled = mc.enabled;
                copy.convex = mc.convex;
                copy.sharedMesh = mc.sharedMesh;
                copy.sharedMaterial = mc.sharedMaterial;
            }
            else if (source.GetType() == typeof(BoxCollider))
            {
                BoxCollider bc = (BoxCollider)source;

                BoxCollider copy = target.AddComponent<BoxCollider>();
                copy.enabled = bc.enabled;
                copy.size = bc.size;
                copy.center = bc.center;
            }
            else if (source.GetType() == typeof(SphereCollider))
            {
                SphereCollider sc = (SphereCollider)source;

                SphereCollider copy = target.AddComponent<SphereCollider>();
                copy.enabled = sc.enabled;
                copy.center = sc.center;
                copy.radius = sc.radius;

            }
            else if(source.GetType() == typeof(CapsuleCollider))
            {
                CapsuleCollider cc = (CapsuleCollider)source;

                CapsuleCollider copy = target.AddComponent<CapsuleCollider>();
                copy.enabled = cc.enabled;
                copy.direction = cc.direction;
                copy.radius = cc.radius;
                copy.height = cc.height;
            }
            else if(source.GetType() == typeof(TerrainCollider))
            {
                TerrainCollider tc = (TerrainCollider)source;

                TerrainCollider copy = target.AddComponent<TerrainCollider>();
                copy.enabled = tc.enabled;
                copy.material = tc.material;
                copy.terrainData = tc.terrainData;
            }
            else
                Debug.LogWarning("Unsupported type of collider (" + source.GetType().Name + ") for copying. Please use either mesh, box or sphere collider.");

            Collider res;
            if (target.TryGetComponent<Collider>(out res))
            {
                res.transform.position = source.transform.position;
                res.transform.rotation = source.transform.rotation;
                res.transform.localScale = source.transform.localScale;

                return true;
            }

            if (useSubObject)
                DestroyImmediate(target);

            return false;
        }

        /// <summary>
        /// Rearranges a present game object to Altspace's specifications.
        /// </summary>
        /// <param name="model">The source game object</param>
        /// <returns>The resulting game object</returns>
        private static GameObject RearrangeGameObject(GameObject model)
        {
            string rootname = Common.SanitizeFileName(model.name);

            GameObject kiRoot = new GameObject(rootname);

            if (SettingsManager.settings.KitUnsetStatic)
                UnsetStatic(model);

            model.transform.SetParent(kiRoot.transform);
            Transform modelTr = kiRoot.transform.GetChild(0);
            modelTr.name = "model";

            GameObject collider = new GameObject("collider");

            // Reset to layer 14 if so demanded, else keep it at the same layer as the model
            if (SettingsManager.settings.KitsSetLayer)
                collider.layer = 14;
            else
                collider.layer = model.layer;

            collider.transform.SetParent(kiRoot.transform);

            modelTr.transform.SetAsFirstSibling();
            Transform colliderTr = kiRoot.transform.GetChild(1);

            // Make Colliders dimension match up with the Model dimensions
            colliderTr.localPosition = modelTr.localPosition;
            colliderTr.localRotation = modelTr.localRotation;
            colliderTr.localScale = modelTr.localScale;

            // Try recreating the collider(s) in the "collider" subobject
            Collider[] colliders = model.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 1)
                Debug.LogWarning("More than one collider found. Could cause problems.");

            bool addedCollider = false;
            foreach (Collider c in colliders)
            {
                addedCollider |= CopyCollider(c, collider, colliders.Length > 1);
                DestroyImmediate(c);
            }

            // If we didn't add a collider at all, add at least a disabled box collider to avoid errors.
            if (!addedCollider)
            {
                Collider def = collider.AddComponent<BoxCollider>();
                def.enabled = false;
            }

            return kiRoot;
        }

        [MenuItem("GameObject/Convert to AltVR kit item...", false, 30)]
        public static void ConvertToKitItem(MenuCommand command)
        {
            if(string.IsNullOrEmpty(OnlineKitManager.kitRoot) || !Directory.Exists(OnlineKitManager.kitRoot))
            {
                EditorUtility.DisplayDialog("Error", "You need to set a valid and existing directory in your Login Management.", "Understood");
                return;
            }

            GameObject[] gos = Selection.gameObjects;
            if(gos.Length < 1)
            {
                EditorUtility.DisplayDialog("Error", "No game object selected.", "Got it!");
                return;
            }

            for(int index = 0;index < gos.Length; index++)
            {
                if(gos.Length > 1)
                    EditorUtility.DisplayProgressBar("Converting Gameobjects to Kit Prefabs", "Please wait...", index / gos.Length);

                GameObject go = gos[index];

                Renderer[] rs = go.GetComponentsInChildren<Renderer>();

                // Add a dummy mesh and meshRenderer (disabled) if there's none found in the game object.
                if(rs.Length < 1)
                {
                    GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);

                    MeshRenderer mr = go.AddComponent<MeshRenderer>();
                    mr.enabled = false;
                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
                    DestroyImmediate(tmp);
                }

                NormalizeGO(go);

                RedoLights(go);

                RedoShaders(go);

                GameObject res = RearrangeGameObject(go);

                string tgtPath = Path.Combine(OnlineKitManager.kitRoot, res.name + ".prefab");
                GameObject prefab = null;

                if(SettingsManager.settings.KitsRemoveWhenGenerated)
                {
                    prefab = PrefabUtility.SaveAsPrefabAsset(res, tgtPath);
                    DestroyImmediate(res);
                }
                else
                    prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(res, tgtPath, InteractionMode.UserAction);


                CreateScreenshot(prefab);
            }

            AssetDatabase.Refresh();
            EditorUtility.ClearProgressBar();
        }

    }

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
            if (SettingsManager.settings.TmplDeleteCameras)
            {
                foreach (Camera camera in FindObjectsOfType<Camera>())
                    DestroyImmediate(camera.gameObject);
            }

            // Add lights to layer 15, if so wanted
            if (SettingsManager.settings.TmplSetLightLayer)
            {
                foreach (Light light in env.GetComponentsInChildren<Light>())
                    light.cullingMask |= (1 << 15) | (1 << 10);
            }

            if (SettingsManager.settings.TmplFixEnviroLight)
            {
                if (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Trilight)
                {
                    // If it's flat lighting, derive the Trilight from the single one
                    if (RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat)
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
            foreach (Light light in env.GetComponentsInChildren<Light>())
            {
                if (light.type == LightType.Directional)
                    noDirectional = false;
            }

            if (noDirectional)
                Debug.LogWarning("There's no directional light. Altspace will add a default white light.");

            EditorSceneManager.SaveScene(sc);

            return tmpSceneName;
        }
    }

}

#endif // UNITY_EDITOR