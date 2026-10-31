using System;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Editor.Sync.AssetFiles;
using UnityEditorOnline.Editor.Sync.Camera;
using UnityEditorOnline.Editor.Sync.Selection;
using UnityEditorOnline.Editor.Sync.Tracking;
using UnityEngine;

namespace UnityEditorOnline.Editor.Sync.Receiving
{
    public static class SyncReceiver
    {
        public static void Handle(string json)
        {
            Envelope env;
            try 
            { 
                env = JsonUtility.FromJson<Envelope>(json); 
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[UnityEditorOnline] Failed to parse the packet: {e.Message}");
                return;
            }

            if (env == null || string.IsNullOrEmpty(env.type)) 
                return;

            SyncTracker.FlushNow();
            SyncTracker.ApplyingRemote = true;

            try
            {
                Dispatch(env);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                SyncTracker.ApplyingRemote = false;
                if (ChangesStructure(env.type)) 
                    SyncTracker.RefreshKnown();
            }
        }

        private static void Dispatch(Envelope env)
        {
            switch (env.type)
            {
                case PacketType.SnapshotBegin: SceneApplier.ClearSynced(); break;
                case PacketType.Create: SceneApplier.Create(JsonUtility.FromJson<CreatePacket>(env.body)); break;
                case PacketType.Destroy: SceneApplier.Destroy(JsonUtility.FromJson<DestroyPacket>(env.body)); break;
                case PacketType.Move: SceneApplier.Move(JsonUtility.FromJson<MovePacket>(env.body)); break;
                case PacketType.Set: SceneApplier.Set(JsonUtility.FromJson<SetPacket>(env.body)); break;
                case PacketType.Components: SceneApplier.Components(JsonUtility.FromJson<ComponentsPacket>(env.body)); break;

                case PacketType.Asset:
                    {
                        AssetPacket ap = JsonUtility.FromJson<AssetPacket>(env.body);
                        if (ap != null) AssetSync.ReceiveRemote(ap.path, ap.content, ap.meta);
                        break;
                    }

                case PacketType.Selection: SelectionSync.ApplyRemote(JsonUtility.FromJson<SelectionPacket>(env.body)); break;
                case PacketType.Camera: CameraSync.ApplyRemote(JsonUtility.FromJson<CameraPacket>(env.body)); break;
                default:
                    Debug.LogWarning($"[UnityEditorOnline] Unknown packet type: {env.type}");
                    break;
            }
        }

        private static bool ChangesStructure(string type)
        {
            return type == PacketType.SnapshotBegin
                || type == PacketType.Create
                || type == PacketType.Destroy
                || type == PacketType.Move
                || type == PacketType.Components;
        }
    }
}