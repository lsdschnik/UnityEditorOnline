using System;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class AssetPacket
    {
        public string path;
        public string content;
        public string meta;
        public bool open;   
    }
}