using System;
using UnityEditorOnline.Editor.Protocols.PacketModels;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class CreatePacket
    {
        public ObjectState[] objects;
    }
}