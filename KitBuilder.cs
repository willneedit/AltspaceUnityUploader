using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{
    [InitializeOnLoad]
    [ExecuteInEditMode]
    public class KitBuilder : MonoBehaviour
    {
        private string rootDir;

        /// <summary>
        /// Copies the collider component onto target, retaining its properties and transform in world space
        /// </summary>
        /// <param name="source">the collider component to copy</param>
        /// <param name="target">the target hierarchy</param>
        /// <param name="useSubObject">place the collider on a sub object, rather than target itself</param>
        /// <returns>true if collider has been added</returns>
        public static bool CopyCollider(Collider source, GameObject target, bool useSubObject)
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

            model.transform.SetParent(kiRoot.transform);
            Transform modelTr = kiRoot.transform.GetChild(0);
            modelTr.name = "model";

            GameObject collider = new GameObject("collider");
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

                // TODO: Setting layer
                // TODO: Setting light layer
                // TODO: Normalization of game object (resetting transform)
                // TODO: Shaders

                // ApplyGameObjectFixes(go);

                GameObject res = RearrangeGameObject(go);

                string tgtPath = Path.Combine(OnlineKitManager.kitRoot, res.name + ".prefab");
                GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(res, tgtPath, InteractionMode.UserAction);
                AssetDatabase.Refresh();

                // TODO: Config: Remove item after conversion
                // DestroyImmediate(res);

                // TODO: Screenshot
            }

            EditorUtility.ClearProgressBar();
        }

        public KitBuilder(string rootDirectory)
            : base()
        {
            rootDir = rootDirectory;
        }


    }
}
