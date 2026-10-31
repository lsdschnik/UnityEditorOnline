using System;
using UnityEditorOnline.Editor.Protocols.PacketModels;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class ComponentsPacket
    {
        public ObjectState state;
    }
}