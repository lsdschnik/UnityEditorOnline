using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Codec;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Sync.Tracking
{
    public static class DirtyFields
    {
        private class DirtyEntry
        {
            public string guid;
            public string type;
            public int index;
            public string path;
        }

        private static readonly Dictionary<string, DirtyEntry> _dirty = new Dictionary<string, DirtyEntry>();

        public static void Clear() => _dirty.Clear();
        
        public static UndoPropertyModification[] OnPostprocess(UndoPropertyModification[] mods)
        {
            if (SyncTracker.ApplyingRemote)
                return mods;

            foreach (UndoPropertyModification m in mods)
            {
                Object target = m.currentValue.target;
                if (target == null)
                    continue;

                GameObject go;
                string typeName;
                int index;

                if (target is GameObject g)
                {
                    go = g; typeName = "GameObject"; index = 0;
                }
                else if (target is Component c)
                {
                    if (c is SyncId)
                        continue;

                    go = c.gameObject; typeName = c.GetType().FullName; index = ComponentUtil.IndexOf(c);
                }
                else 
                    continue;

                SyncId id = go.GetComponent<SyncId>();
                if (id == null || string.IsNullOrEmpty(id.Guid))
                    continue; 

                AddDirty(id.Guid, typeName, index, m.currentValue.propertyPath);
            }
            return mods;
        }

        public static void AddDirty(string guid, string type, int index, string path)
        {
            _dirty[guid + "|" + type + "|" + index + "|" + path] =
                new DirtyEntry { guid = guid, type = type, index = index, path = path };
        }

        public static void MarkAllDirty(GameObject go)
        {
            SyncId id = go.GetComponent<SyncId>();

            if (id == null) 
                return;

            foreach (FieldValue f in PropertyCodec.Snapshot(go, true))
                AddDirty(id.Guid, "GameObject", 0, f.path);

            foreach (ComponentEntry e in ComponentUtil.GetAll(go))
                foreach (FieldValue f in PropertyCodec.Snapshot(e.component, false))
                    AddDirty(id.Guid, e.type, e.index, f.path);
        }

        public static void FlushDirty()
        {
            if (_dirty.Count == 0) 
                return;

            var groups = new Dictionary<string, List<DirtyEntry>>();
            foreach (DirtyEntry d in _dirty.Values)
            {
                string key = d.guid + "|" + d.type + "|" + d.index;
                if (!groups.TryGetValue(key, out List<DirtyEntry> list))
                    groups[key] = list = new List<DirtyEntry>();
                list.Add(d);
            }
            _dirty.Clear();

            foreach (List<DirtyEntry> list in groups.Values)
            {
                DirtyEntry first = list[0];
                Object target = ComponentUtil.Find(SyncRegistry.Find(first.guid), first.type, first.index);
                if (target == null) 
                    continue;

                var so = new SerializedObject(target);
                var fields = new List<FieldValue>();
                foreach (DirtyEntry d in list)
                {
                    SerializedProperty p = so.FindProperty(d.path);
                    if (p != null && PropertyCodec.TryEncode(p, out string value))
                        fields.Add(new FieldValue { path = d.path, value = value }); 
                }

                if (fields.Count > 0)
                {
                    SyncTracker.Send(PacketType.Set, new SetPacket
                    {
                        guid = first.guid,
                        type = first.type,
                        index = first.index,
                        fields = fields.ToArray()
                    });
                }
            }
        }
    }
}