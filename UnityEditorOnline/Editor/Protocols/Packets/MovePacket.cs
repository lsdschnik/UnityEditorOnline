using System;
using UnityEditorOnline.Editor.Protocols.PacketModels;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class MovePacket
    {
        public MoveEntry[] moves;
    }
}