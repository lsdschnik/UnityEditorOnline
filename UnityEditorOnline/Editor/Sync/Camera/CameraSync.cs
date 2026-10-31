using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Editor.Sync.Selection;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Camera
{
    public static class CameraSync
    {
        public class RemoteCamera
        {
            public Color color;
            public Vector3 position;
            public Quaternion rotation;
        }

        public static Dictionary<string, RemoteCamera> Remote { get; } = new Dictionary<string, RemoteCamera>();

        public static event Action RemoteChanged;

        private const float MinMove = 0.01f;     
        private const float MinAngle = 0.2f;     

        private static bool _running;
        private static bool _force;             
        private static Vector3 _lastPos;
        private static Quaternion _lastRot = Quaternion.identity;

        private static string UserId => SelectionSync.LocalUserId;

        public static void Start()
        {
            if (_running) 
                return;
            _running = true;

            _force = true; 
        }

        public static void Stop()
        {
            if (!_running) 
                return;
            _running = false;

            Clear();
        }

        public static void MarkDirty() => _force = true;
        
        public static void Flush()
        {
            if (!_running || string.IsNullOrEmpty(UserId)) 
                return;

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null) 
                return;

            Transform t = view.camera.transform;
            Vector3 pos = t.position;
            Quaternion rot = t.rotation;

            bool moved = 
                (pos - _lastPos).sqrMagnitude > MinMove * MinMove 
                || Quaternion.Angle(rot, _lastRot) > MinAngle;

            if (!_force && !moved) 
                return;

            _force = false;
            _lastPos = pos;
            _lastRot = rot;

            SyncTracker.Send(PacketType.Camera, new CameraPacket
            {
                user = UserId,
                color = SelectionSync.LocalColor,
                position = pos,
                rotation = rot
            });
        }

        public static void ApplyRemote(CameraPacket p)
        {
            if (p == null || string.IsNullOrEmpty(p.user) || p.user == UserId) 
                return;

            if (p.left) 
                Remote.Remove(p.user);
            else 
                Remote[p.user] = new RemoteCamera { color = p.color, position = p.position, rotation = p.rotation };

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

        public static string BuildLeavePacket(string user)
        {
            return PacketIO.Wrap(PacketType.Camera,
                new CameraPacket { user = user, color = Color.white, rotation = Quaternion.identity, left = true });
        }

        public static List<string> BuildSnapshotPackets()
        {
            var result = new List<string>();
            foreach (KeyValuePair<string, RemoteCamera> kv in Remote)
                result.Add(PacketIO.Wrap(PacketType.Camera, new CameraPacket
                {
                    user = kv.Key,
                    color = kv.Value.color,
                    position = kv.Value.position,
                    rotation = kv.Value.rotation
                }));
            return result;
        }
    }
}