using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorOnline.Editor.Codec;
using UnityEditorOnline.Editor.Protocols;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEditorOnline.Editor.Protocols.Packets;
using UnityEditorOnline.Editor.Sync.Tracking;

namespace UnityEditorOnline.Editor.Sync.AssetFiles
{
    public static class AssetSync
    {
        private static readonly string[] Extensions = { ".anim", ".controller" };

        private static readonly Dictionary<string, int> LastHash = new Dictionary<string, int>();

        public static bool IsSyncable(string path)
        {
            if (!path.StartsWith("Assets/"))
                return false;

            foreach (string e in Extensions)
                if (path.EndsWith(e))
                    return true;

            return false;
        }

        public static void SendLocal(string path)
        {
            if (!SyncTracker.IsActive || SyncTracker.ApplyingRemote) 
                return;

            string meta = path + ".meta";
            if (!File.Exists(path) || !File.Exists(meta)) 
                return;

            string content = File.ReadAllText(path);
            string metaText = File.ReadAllText(meta);

            int hash = (content + metaText).GetHashCode();
            if (LastHash.TryGetValue(path, out int old) && old == hash) 
                return; 

            LastHash[path] = hash;

            SyncTracker.SendRaw(PacketIO.Wrap(PacketType.Asset,
                new AssetPacket { path = path, content = content, meta = metaText }));
        }

        public static void ReceiveRemote(string path, string content, string metaText)
        {
            if (!IsSyncable(path)) 
                return;

            LastHash[path] = (content + metaText).GetHashCode(); 

            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string meta = path + ".meta";
            if (!File.Exists(meta)) 
                File.WriteAllText(meta, metaText);

            File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            PropertyCodec.RetryPending(); 
        }

    }
}
