using System;

namespace UnityEditorOnline.Editor.Protocols.PacketModels
{
    [Serializable]
    public class MoveEntry
    {
        public string guid;
        public string parent;
        public int sibling;
    }
}