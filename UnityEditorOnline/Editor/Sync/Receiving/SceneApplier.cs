using System;
using System.Collections.Generic;
using UnityEditorOnline.Editor.Codec;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Sync.Receiving
{
    public static class SceneApplier
    {
        public static void ClearSynced()
        {
            foreach (GameObject go in SyncRegistry.All())
                if (go != null) 
                    Object.DestroyImmediate(go);

            SyncRegistry.Rebuild();
        }

        public static void Create(CreatePacket p)
        {
            if (p == null || p.objects == null) 
                return;

            var made = new List<KeyValuePair<GameObject, ObjectState>>();

            foreach (ObjectState st in p.objects)
            {
                if (SyncRegistry.Find(st.guid) != null) 
                    continue; 

                var go = new GameObject("GameObject");
                go.AddComponent<SyncId>().Assign(st.guid);
                SyncRegistry.Register(st.guid, go);

                GameObject parent = SyncRegistry.Find(st.parent);
                go.transform.SetParent(parent != null ? parent.transform : null, false);
                go.transform.SetSiblingIndex(st.sibling);

                made.Add(new KeyValuePair<GameObject, ObjectState>(go, st));
            }

            foreach (KeyValuePair<GameObject, ObjectState> kv in made)
            {
                if (kv.Value.components == null) 
                    continue;

                foreach (ComponentState cs in kv.Value.components)
                {
                    Object target;
                    if (cs.type == "GameObject")
                    {
                        target = kv.Key;
                    }
                    else
                    {
                        target = ComponentUtil.Find(kv.Key, cs.type, cs.index);
                        if (target == null)
                        {
                            Type type = ComponentUtil.ResolveType(cs.type);
                            if (type == null || !typeof(Component).IsAssignableFrom(type))
                            {
                                Debug.LogWarning($"[UnityEditorOnline] You do not have component {cs.type} — skipped");
                                continue;
                            }
                            target = kv.Key.AddComponent(type);
                        }
                    }
                    PropertyCodec.Apply(target, cs.fields);
                }
            }

            PropertyCodec.RetryPending();
        }

        public static void Destroy(DestroyPacket p)
        {
            if (p == null || p.guids == null) 
                return;

            foreach (string guid in p.guids)
            {
                GameObject go = SyncRegistry.Find(guid);
                if (go != null) 
                    Object.DestroyImmediate(go);
            }
        }

        public static void Move(MovePacket p)
        {
            if (p == null || p.moves == null) 
                return;

            foreach (MoveEntry m in p.moves)
            {
                GameObject go = SyncRegistry.Find(m.guid);
                if (go == null) 
                    continue;

                GameObject parent = SyncRegistry.Find(m.parent);
                go.transform.SetParent(parent != null ? parent.transform : null, false);
                go.transform.SetSiblingIndex(m.sibling);
            }
        }

        public static void Set(SetPacket p)
        {
            if (p == null) 
                return;

            Object target = ComponentUtil.Find(SyncRegistry.Find(p.guid), p.type, p.index);
            if (target == null)
            {
                Debug.LogWarning($"[UnityEditorOnline] set: not found {p.type}[{p.index}] of the object {p.guid}");
                return;
            }
            PropertyCodec.Apply(target, p.fields);
        }

        public static void Components(ComponentsPacket p)
        {
            if (p == null || p.state == null) 
                return;

            GameObject go = SyncRegistry.Find(p.state.guid);
            if (go == null) 
                return;

            Dictionary<string, int> want = CountWanted(p.state.components);
            RemoveExtra(go, want);
            AddMissing(go, want);

            foreach (ComponentState cs in p.state.components)
            {
                Object target = ComponentUtil.Find(go, cs.type, cs.index);
                if (target != null) 
                    PropertyCodec.Apply(target, cs.fields);
            }

            PropertyCodec.RetryPending();
        }

        private static Dictionary<string, int> CountWanted(ComponentState[] components)
        {
            var want = new Dictionary<string, int>();
            if (components == null) 
                return want;

            foreach (ComponentState cs in components)
            {
                if (cs.type == "GameObject") 
                    continue;
                want.TryGetValue(cs.type, out int n);
                want[cs.type] = Math.Max(n, cs.index + 1);
            }
            return want;
        }

        private static void RemoveExtra(GameObject go, Dictionary<string, int> want)
        {
            var toRemove = new List<Component>();

            foreach (ComponentEntry e in ComponentUtil.GetAll(go))
            {
                if (e.component is Transform) 
                    continue;

                if (!want.TryGetValue(e.type, out int w) || e.index >= w) 
                    toRemove.Add(e.component);
            }

            for (int i = toRemove.Count - 1; i >= 0; i--)
                Object.DestroyImmediate(toRemove[i]);
        }

        private static void AddMissing(GameObject go, Dictionary<string, int> want)
        {
            foreach (KeyValuePair<string, int> kv in want)
            {
                Type type = ComponentUtil.ResolveType(kv.Key);

                if (type == null || !typeof(Component).IsAssignableFrom(type))
                {
                    Debug.LogWarning($"[UnityEditorOnline] You do not have component {kv.Key} — skipped.");
                    continue;
                }

                if (type == typeof(Transform) || type == typeof(SyncId)) 
                    continue;

                for (int have = CountOf(go, kv.Key); have < kv.Value; have++)
                    go.AddComponent(type);
            }
        }

        private static int CountOf(GameObject go, string typeName)
        {
            int n = 0;
            foreach (ComponentEntry e in ComponentUtil.GetAll(go))
                if (e.type == typeName) 
                    n++;
            return n;
        }
    }
}