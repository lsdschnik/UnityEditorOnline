using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Runtime;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Selection
{
    public static class SelectionSync
    {
        public static string LocalUserId { get; set; } = string.Empty;
        public static Color LocalColor { get; set; } = Color.cyan;

        public class RemoteSelection
        {
            public Color color;
            public string[] guids;
        }

        public static Dictionary<string, RemoteSelection> Remote { get; } = new Dictionary<string, RemoteSelection>();

        public static event Action RemoteChanged;

        private static bool _running;
        private static bool _localDirty;

        public static void Start()
        {
            if (_running) 
                return;
            _running = true;

            UnityEditor.Selection.selectionChanged += OnSelectionChanged;

            _localDirty = true; 
        }

        public static void Stop()
        {
            if (!_running) 
                return;
            _running = false;

            UnityEditor.Selection.selectionChanged -= OnSelectionChanged;

            Clear();
        }

        public static void MarkDirty() => _localDirty = true;

        private static void OnSelectionChanged() => _localDirty = true;
        
        public static void Flush()
        {
            if (!_localDirty) 
                return;
            _localDirty = false;

            if (string.IsNullOrEmpty(LocalUserId)) 
                return;

            var guids = new List<string>();
            foreach (GameObject go in UnityEditor.Selection.gameObjects)
            {
                if (go == null || !go.scene.IsValid()) continue;
                SyncId id = go.GetComponent<SyncId>();
                if (id != null && !string.IsNullOrEmpty(id.Guid)) guids.Add(id.Guid);
            }

            SyncTracker.Send(PacketType.Selection, new SelectionPacket
            {
                user = LocalUserId,
                color = LocalColor,
                guids = guids.ToArray()
            });
        }


        public static void ApplyRemote(SelectionPacket p)
        {
            if (p == null || string.IsNullOrEmpty(p.user) || p.user == LocalUserId)
                return;

            if (p.guids == null || p.guids.Length == 0) 
                Remote.Remove(p.user);
            else 
                Remote[p.user] = new RemoteSelection { color = p.color, guids = p.guids };

            RemoteChanged?.Invoke();
        }

        public static void RemoveUser(string userId)
        {
            if (Remote.Remove(userId))
                RemoteChanged?.Invoke();
        }

        public static void Clear()
        {
            Remote.Clear();
            RemoteChanged?.Invoke();
        }

        public static bool TryGetIdentity(string json, out string user, out Color color)
        {
            user = null;
            color = Color.white;

            try
            {
                Envelope env = JsonUtility.FromJson<Envelope>(json);
                if (env == null || env.type != PacketType.Selection) 
                    return false;

                SelectionPacket p = JsonUtility.FromJson<SelectionPacket>(env.body);
                if (p == null || string.IsNullOrEmpty(p.user)) 
                    return false;

                user = p.user;
                color = p.color;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string BuildLeavePacket(string user)
        {
            return PacketIO.Wrap(PacketType.Selection,
                new SelectionPacket { user = user, color = Color.white, guids = new string[0] });
        }

        public static List<string> BuildSnapshotPackets()
        {
            var result = new List<string>();

            foreach (KeyValuePair<string, RemoteSelection> kv in Remote)
                result.Add(PacketIO.Wrap(PacketType.Selection,
                    new SelectionPacket { user = kv.Key, color = kv.Value.color, guids = kv.Value.guids }));

            return result;
        }
    }
}