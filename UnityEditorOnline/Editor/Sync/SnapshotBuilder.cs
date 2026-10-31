using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Codec;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Runtime;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync
{
    public static class SnapshotBuilder
    {
        private const int ChunkSize = 50; 

        public static List<string> BuildFullSnapshot()
        {
            SyncRegistry.Rebuild();

            var result = new List<string> { PacketIO.Wrap(PacketType.SnapshotBegin) };

            var objects = new List<GameObject>();
            foreach (GameObject go in SyncRegistry.All())
                objects.Add(go);

            result.AddRange(BuildCreatePackets(objects));
            return result;
        }

        public static List<string> BuildCreatePackets(List<GameObject> objects)
        {
            var packets = new List<string>();
            if (objects.Count == 0) 
                return packets;

            objects.Sort((a, b) =>
            {
                int c = Depth(a).CompareTo(Depth(b));
                return c != 0 ? c : a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex());
            });

            for (int i = 0; i < objects.Count; i += ChunkSize)
            {
                int n = Math.Min(ChunkSize, objects.Count - i);
                var states = new ObjectState[n];

                for (int j = 0; j < n; j++) 
                    states[j] = Capture(objects[i + j]);

                packets.Add(PacketIO.Wrap(PacketType.Create, new CreatePacket { objects = states }));
            }
            return packets;
        }

        public static ObjectState Capture(GameObject go)
        {
            var components = new List<ComponentState>
            {
                new ComponentState { type = "GameObject", index = 0, fields = PropertyCodec.Snapshot(go, true) }
            };

            foreach (ComponentEntry e in ComponentUtil.GetAll(go))
                components.Add(new ComponentState
                {
                    type = e.type,
                    index = e.index,
                    fields = PropertyCodec.Snapshot(e.component, false)
                });

            return new ObjectState
            {
                guid = go.GetComponent<SyncId>().Guid,
                parent = SyncRegistry.ParentGuid(go),
                sibling = go.transform.GetSiblingIndex(),
                components = components.ToArray()
            };
        }

        private static int Depth(GameObject go)
        {
            int depth = 0;
            for (Transform p = go.transform.parent; p != null; p = p.parent) 
                depth++;
            return depth;
        }
    }
}