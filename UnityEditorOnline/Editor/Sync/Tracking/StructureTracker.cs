using System.Collections.Generic;
using System.Text;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Runtime;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Tracking
{
    public static class StructureTracker
    {
        private class Node
        {
            public string parent;
            public int sibling;
            public string comps;
        }

        public static bool Dirty { get; set; }

        private static Dictionary<string, Node> _known = new Dictionary<string, Node>();
        public static void Reset() => _known = CaptureStructure();
        public static void Clear()
        {
            _known.Clear();
            Dirty = false;
        }

        public static void Refresh()
        {
            SyncRegistry.Rebuild();
            _known = CaptureStructure();
        }

        public static void Flush()
        {
            Dirty = false;

            SyncRegistry.Rebuild();
            SyncRegistry.AssignMissingIds();
            Dictionary<string, Node> current = CaptureStructure();

            var destroyed = new List<string>();
            foreach (string guid in _known.Keys)
                if (!current.ContainsKey(guid)) 
                    destroyed.Add(guid);

            var created = new List<GameObject>();
            var moves = new List<MoveEntry>();
            var compChanged = new List<GameObject>();

            foreach (KeyValuePair<string, Node> kv in current)
            {
                if (!_known.TryGetValue(kv.Key, out Node old))
                {
                    GameObject go = SyncRegistry.Find(kv.Key);
                    if (go != null) 
                        created.Add(go);
                }
                else
                {
                    if (old.parent != kv.Value.parent || old.sibling != kv.Value.sibling)
                    {
                        moves.Add(new MoveEntry { guid = kv.Key, parent = kv.Value.parent, sibling = kv.Value.sibling });

                        if (old.parent != kv.Value.parent)
                        {
                            DirtyFields.AddDirty(kv.Key, "UnityEngine.Transform", 0, "m_LocalPosition");
                            DirtyFields.AddDirty(kv.Key, "UnityEngine.Transform", 0, "m_LocalRotation");
                            DirtyFields.AddDirty(kv.Key, "UnityEngine.Transform", 0, "m_LocalScale");
                        }
                    }

                    if (old.comps != kv.Value.comps)
                    {
                        GameObject go = SyncRegistry.Find(kv.Key);
                        if (go != null) compChanged.Add(go);
                    }
                }
            }

            _known = current;

            foreach (GameObject go in compChanged)
                SyncTracker.Send(PacketType.Components, new ComponentsPacket { state = SnapshotBuilder.Capture(go) });

            if (destroyed.Count > 0)
                SyncTracker.Send(PacketType.Destroy, new DestroyPacket { guids = destroyed.ToArray() });

            foreach (string json in SnapshotBuilder.BuildCreatePackets(created))
                SyncTracker.SendRaw(json);

            if (moves.Count > 0)
            {
                moves.Sort((a, b) =>
                {
                    int c = string.CompareOrdinal(a.parent, b.parent);
                    return c != 0 ? c : a.sibling.CompareTo(b.sibling);
                });
                SyncTracker.Send(PacketType.Move, new MovePacket { moves = moves.ToArray() });
            }
        }

        private static Dictionary<string, Node> CaptureStructure()
        {
            var result = new Dictionary<string, Node>();
            foreach (GameObject go in SyncRegistry.All())
            {
                SyncId id = go.GetComponent<SyncId>();

                if (id == null) 
                    continue;

                result[id.Guid] = new Node
                {
                    parent = SyncRegistry.ParentGuid(go),
                    sibling = go.transform.GetSiblingIndex(),
                    comps = ComponentUtil.Signature(go)
                };
            }
            return result;
        }

    }
}