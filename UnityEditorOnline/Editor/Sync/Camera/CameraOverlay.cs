using System.Collections.Generic;
using UnityEditor;
using UnityEditorOnline.Editor.Sync.Camera;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Camera
{
    internal static class CameraOverlay
    {
        private const float SmoothSpeed = 14f;

        private class Shown
        {
            public Vector3 position;
            public Quaternion rotation;
        }

        private static readonly Dictionary<string, Shown> ShownPoses = new Dictionary<string, Shown>();
        private static double _lastTick;
        private static GUIStyle _labelStyle;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            SceneView.duringSceneGui += OnSceneGUI;
            EditorApplication.update += Tick;
            CameraSync.RemoteChanged += SceneView.RepaintAll;
        }

        private static void Tick()
        {
            if (CameraSync.Remote.Count == 0)
            {
                if (ShownPoses.Count > 0) ShownPoses.Clear();
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Min((float)(now - _lastTick), 0.1f);
            _lastTick = now;

            float k = 1f - Mathf.Exp(-SmoothSpeed * dt);
            bool moving = false;

            foreach (KeyValuePair<string, CameraSync.RemoteCamera> kv in CameraSync.Remote)
            {
                if (!ShownPoses.TryGetValue(kv.Key, out Shown s))
                {
                    ShownPoses[kv.Key] = new Shown { position = kv.Value.position, rotation = kv.Value.rotation };
                    moving = true;
                    continue;
                }

                Vector3 newPos = Vector3.Lerp(s.position, kv.Value.position, k);
                Quaternion newRot = Quaternion.Slerp(s.rotation, kv.Value.rotation, k);

                if ((newPos - s.position).sqrMagnitude > 1e-8f || Quaternion.Angle(newRot, s.rotation) > 0.01f)
                    moving = true;

                s.position = newPos;
                s.rotation = newRot;
            }

            List<string> gone = null;
            foreach (string nick in ShownPoses.Keys)
            {
                if (CameraSync.Remote.ContainsKey(nick)) continue;
                if (gone == null) 
                    gone = new List<string>();
                gone.Add(nick);
            }
            if (gone != null)
            {
                foreach (string nick in gone) 
                    ShownPoses.Remove(nick);
                moving = true;
            }

            if (moving) SceneView.RepaintAll(); 
        }

        private static void OnSceneGUI(SceneView view)
        {
            if (Event.current.type != EventType.Repaint || ShownPoses.Count == 0) 
                return;

            if (_labelStyle == null)
                _labelStyle = new GUIStyle(EditorStyles.boldLabel);

            foreach (KeyValuePair<string, Shown> kv in ShownPoses)
            {
                if (!CameraSync.Remote.TryGetValue(kv.Key, out CameraSync.RemoteCamera cam)) 
                    continue;

                DrawCamera(kv.Key, kv.Value.position, kv.Value.rotation, cam.color);
            }
        }

        private static void DrawCamera(string nick, Vector3 pos, Quaternion rot, Color color)
        {
            float s = HandleUtility.GetHandleSize(pos) * 0.6f;
            float halfW = s * 0.55f;
            float halfH = s * 0.35f;

            Vector3 tl = pos + rot * new Vector3(-halfW, halfH, s);
            Vector3 tr = pos + rot * new Vector3(halfW, halfH, s);
            Vector3 br = pos + rot * new Vector3(halfW, -halfH, s);
            Vector3 bl = pos + rot * new Vector3(-halfW, -halfH, s);
            Vector3 peak = pos + rot * new Vector3(0f, halfH + s * 0.25f, s); 

            using (new Handles.DrawingScope(color))
            {
                Handles.DrawAAPolyLine(2.5f, pos, tl);
                Handles.DrawAAPolyLine(2.5f, pos, tr);
                Handles.DrawAAPolyLine(2.5f, pos, br);
                Handles.DrawAAPolyLine(2.5f, pos, bl);

                Handles.DrawAAPolyLine(2.5f, tl, tr, br, bl, tl);
                Handles.DrawAAPolyLine(2.5f, tl, peak, tr);

                Handles.SphereHandleCap(0, pos, Quaternion.identity, s * 0.18f, EventType.Repaint);
            }

            _labelStyle.normal.textColor = color;
            Handles.Label(pos + Vector3.up * s * 0.4f, nick, _labelStyle);
        }
    }
}