#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AltSpace_Unity_Uploader
{

    public class CreateWindowBase : EditorWindow
    {
        protected delegate bool check_fn();

        protected Action m_commitAction;

        public void SetCommitAction(Action c) { m_commitAction = c; }
        protected void Trailer(check_fn cc) {
            EditorGUILayout.Space(10);

            EditorGUILayout.BeginHorizontal();
            if (cc())
            {
                if (GUILayout.Button("Create!"))
                {
                    m_commitAction();
                    Close();
                }
            }

            if (GUILayout.Button("Abort"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }


    }

    public abstract class OnlineManagerBase<T, U, V> : EditorWindow 
        where T: AltspaceListItem, new() 
        where U: ITypedAsset, new()
        where V: EditorWindow
    {
        protected static Dictionary<string, T> _known_items = new Dictionary<string, T>();
        protected static T _selected_item = new T();

        public void SelectItem(string id)
        {
            _selected_item = _known_items[id];
            this.Close();
            GetWindow<LoginManager>().Repaint();
        }

        public static void ResetContents()
        {
            GetWindow<V>().Close();
            _known_items = new Dictionary<string, T>();
            _selected_item = new T();
        }

        protected static void EnterItemData(U itemJSON)
        {
            if(!String.IsNullOrEmpty(itemJSON.assetName))
            {
                _known_items.Remove(itemJSON.assetId);
                T new_item = new T();
                new_item.importAltVRItem(itemJSON);
                _known_items.Add(itemJSON.assetId, new_item);
            }
        }

        protected static bool LoadSingleItem(string item_id)
        {
            U itemJSON = LoginManager.LoadSingleAltVRItem<U>(item_id);
            if(itemJSON != null && !String.IsNullOrEmpty(itemJSON.assetName))
            {
                EnterItemData(itemJSON);
                return true;
            }

            return false;
        }

        protected void LoadItems<W>() where W: IPaginated, new()
        {
            _known_items.Clear();
            LoginManager.LoadAltVRItems((W content) => content.iterator<U>(EnterItemData));

            if (_known_items.Count < 1)
                ShowNotification(new GUIContent("Item list is empty"), 5.0f);
        }

        protected static void ManageItems(string message)
        {

            AltVRItemWidgets.ManageItem(
                _selected_item,
                () => GetWindow<V>().Show(),
                (string id) => { LoadSingleItem(id); _selected_item = _known_items[id]; },
                message);
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

            void UploadFlatItem(AltspaceListItem subItem, Action<string> updategui_fn)
            {
                LoginManager.ManageAltVRItem(subItem);

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

            bool wouldUploadAssetBundle = false;

            if (!item.isSet)
                GUILayout.Label(missingString, new GUIStyle()
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
            else if (item.exists)
            {

                if (item.isAssetBundleItem)
                {
                    if (GUILayout.Button("Build"))
                        EditorApplication.delayCall += () => BuildItem(item);

                    if (item.isSelected)
                    {
                        if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null)
                        {
                            if (GUILayout.Button("Build & Upload"))
                                EditorApplication.delayCall += () => BuildAndUploadItem(item, updateItem_fn);
                        }
                        else wouldUploadAssetBundle = true;
                    }
                }
                else
                {
                    if (GUILayout.Button("Upload"))
                        EditorApplication.delayCall += () => UploadFlatItem(item, updateItem_fn);
                }

            }

            EditorGUILayout.EndHorizontal();
            if (wouldUploadAssetBundle)
                GUILayout.Label(
                    "The Universal Rendering Pipeline (URP) is not configured,\n" +
                    "please set up manually or 'Convert to URP' in the Settings dialog to enable uploading."
                    , new GUIStyle() { fontStyle = FontStyle.Bold });
        }

    }
}

#endif // UNITY_EDITOR
