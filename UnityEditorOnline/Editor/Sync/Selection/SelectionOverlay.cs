using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Selection
{
    internal static class SelectionOverlay
    {
        private static readonly Dictionary<int, Color> _rowColors = new Dictionary<int, Color>(); 

        private static bool _rowsDirty;
        private static GUIStyle _labelStyle;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyItem;
            EditorApplication.hierarchyChanged += () => _rowsDirty = true;
            SelectionSync.RemoteChanged += OnRemoteChanged;
        }

        private static void OnRemoteChanged()
        {
            _rowsDirty = true;
            EditorApplication.RepaintHierarchyWindow();
            SceneView.RepaintAll();
        }

        private static void RebuildRows()
        {
            _rowsDirty = false;
            _rowColors.Clear();

            foreach (SelectionSync.RemoteSelection sel in SelectionSync.Remote.Values)
            {
                foreach (string guid in sel.guids)
                {
                    GameObject go = SyncRegistry.Find(guid);
                    if (go != null) 
                        _rowColors[go.GetInstanceID()] = sel.color;
                }
            }
        }

        private static void OnHierarchyItem(int instanceID, Rect rect)
        {
            if (SelectionSync.Remote.Count == 0) 
                return;
            if (_rowsDirty) 
                RebuildRows();

            if (!_rowColors.TryGetValue(instanceID, out Color c)) 
                return;

            EditorGUI.DrawRect(rect, new Color(c.r, c.g, c.b, 0.25f));          
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), c);   
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (SelectionSync.Remote.Count == 0 || Event.current.type != EventType.Repaint) 
                return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(EditorStyles.boldLabel);

            foreach (KeyValuePair<string, SelectionSync.RemoteSelection> kv in SelectionSync.Remote)
            {
                Color c = kv.Value.color;
                _labelStyle.normal.textColor = c;
                Handles.color = c;

                bool labeled = false; 

                foreach (string guid in kv.Value.guids)
                {
                    GameObject go = SyncRegistry.Find(guid);

                    if (go == null || !go.activeInHierarchy) 
                        continue;

                    Bounds b = GetBounds(go);
                    Handles.DrawWireCube(b.center, b.size);

                    if (!labeled)
                    {
                        Handles.Label(b.center + Vector3.up * b.extents.y, kv.Key, _labelStyle);
                        labeled = true;
                    }
                }
            }
        }

        private static Bounds GetBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one * 0.5f);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) 
                b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}