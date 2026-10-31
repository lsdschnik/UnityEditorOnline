using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Sync
{
    public static class SyncRegistry
    {
        private static Dictionary<string, GameObject> _map = new Dictionary<string, GameObject>();

        public static GameObject Find(string guid)
        {
            if (string.IsNullOrEmpty(guid))
                return null;
            return _map.TryGetValue(guid, out GameObject go) && go != null ? go : null;
        }

        public static void Register(string guid, GameObject go)
        {
            _map[guid] = go;
        }

        public static List<GameObject> All()
        {
            var list = new List<GameObject>();
            foreach (GameObject go in _map.Values)
                if (go != null) 
                    list.Add(go);
            return list;
        }

        public static string ParentGuid(GameObject go)
        {
            Transform parent = go.transform.parent;
            if (parent == null) 
                return "";
            SyncId id = parent.GetComponent<SyncId>();
            return id != null ? id.Guid : "";
        }
        public static void Rebuild()
        {
            GameObject[] allSceneObjects = Object.FindObjectsOfType<GameObject>(true);
            foreach (GameObject go in allSceneObjects)
            {
                if (!go.scene.IsValid()) 
                    continue;

                if (go.CompareTag("EditorOnly")) 
                    continue;
                if (SceneVisibilityManager.instance.IsHidden(go)) 
                    continue;

                if (go.GetComponent<SyncId>() == null)
                {
                    SyncId id = go.AddComponent<SyncId>();
                    id.Assign(SyncId.NewGuid());
                    EditorUtility.SetDirty(id);
                }
            }

            SyncId[] all = Object.FindObjectsOfType<SyncId>(true);
            var fresh = new Dictionary<string, GameObject>();

            foreach (SyncId id in all)
            {
                string g = id.Guid;
                if (!string.IsNullOrEmpty(g) && _map.TryGetValue(g, out GameObject owner) && owner == id.gameObject)
                    fresh[g] = id.gameObject;
            }

            foreach (SyncId id in all)
            {
                string g = id.Guid;
                if (!string.IsNullOrEmpty(g) && fresh.TryGetValue(g, out GameObject o) && o == id.gameObject) 
                    continue;

                if (string.IsNullOrEmpty(g) || fresh.ContainsKey(g))
                {
                    g = SyncId.NewGuid();
                    id.Assign(g);
                    EditorUtility.SetDirty(id);
                }
                fresh[g] = id.gameObject;
            }

            _map = fresh;
        }

        public static void AssignMissingIds()
        {
            foreach (GameObject go in All())
                AssignRecursive(go.transform);
        }

        private static void AssignRecursive(Transform t)
        {
            foreach (Transform child in t)
            {
                if (child.GetComponent<SyncId>() == null)
                {
                    SyncId id = child.gameObject.AddComponent<SyncId>();
                    id.Assign(SyncId.NewGuid());
                    EditorUtility.SetDirty(id);
                    _map[id.Guid] = child.gameObject;
                }
                AssignRecursive(child);
            }
        }

        public static void SyncObjects()
        {
            Rebuild();
            AssignMissingIds();
        }
    }
}