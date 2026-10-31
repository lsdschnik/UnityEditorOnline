using System;
using UnityEditorOnline.Editor.Protocols.PacketModels;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class SetPacket
    {
        public string guid;
        public string type;
        public int index;
        public FieldValue[] fields;
    }
}