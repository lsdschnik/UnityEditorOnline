using System;
using UnityEditor;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Sync.Camera;
using UnityEditorOnline.Editor.Sync.Selection;
using UnityEditorOnline.Editor.Sync.Tracking;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync
{
    public static class SyncTracker
    {
        private const float FlushInterval = 0.05f;

        public static bool ApplyingRemote { get; set; }
        public static bool IsActive { get; private set; }

        private static Action<string> _send;
        private static float _lastFlush;

        public static void Start(Action<string> send)
        {
            if (IsActive)
                Stop();

            SyncRegistry.SyncObjects();

            _send = send;
            StructureTracker.Reset();
            DirtyFields.Clear();
            IsActive = true;

            SelectionSync.Start();
            CameraSync.Start();

            Undo.postprocessModifications += DirtyFields.OnPostprocess;
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorApplication.update += OnUpdate;
        }

        public static void Stop()
        {
            if (!IsActive)
                return;

            IsActive = false;

            Undo.postprocessModifications -= DirtyFields.OnPostprocess;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.update -= OnUpdate;

            DirtyFields.Clear();
            StructureTracker.Clear();

            SelectionSync.Stop();
            CameraSync.Stop();

            _send = null;
        }

        public static void NotifyStructureChanged() => StructureTracker.Dirty = true;
        private static void OnHierarchyChanged()
        {
            if (!ApplyingRemote)
                StructureTracker.Dirty = true;
        }

        private static void OnUndoRedo()
        {
            StructureTracker.Dirty = true;
            foreach (GameObject go in UnityEditor.Selection.gameObjects)
                DirtyFields.MarkAllDirty(go);
        }

        private static void OnUpdate()
        {
            if (!IsActive || ApplyingRemote) return;

            float now = (float)EditorApplication.timeSinceStartup;
            if (now - _lastFlush < FlushInterval) 
                return;
            _lastFlush = now;

            FlushNow();
        }

        public static void FlushNow()
        {
            if (!IsActive)
                return;

            if (StructureTracker.Dirty)
                StructureTracker.Flush();

            DirtyFields.FlushDirty();
            SelectionSync.Flush();
            CameraSync.Flush();
        }

        public static void Send<T>(string type, T packet) => _send?.Invoke(PacketIO.Wrap(type, packet));
        public static void SendRaw(string json) => _send?.Invoke(json);
        public static void RefreshKnown()
        {
            if (IsActive)
                StructureTracker.Refresh();
        }

    }
}