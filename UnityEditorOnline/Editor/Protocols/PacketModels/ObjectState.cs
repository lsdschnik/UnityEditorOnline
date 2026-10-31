using System;

namespace UnityEditorOnline.Editor.Protocols.PacketModels
{
    [Serializable]
    public class ObjectState
    {
        public string guid;
        public string parent;   
        public int sibling;    
        public ComponentState[] components;
    }
}