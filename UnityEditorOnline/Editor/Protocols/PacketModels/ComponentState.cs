using System;

namespace UnityEditorOnline.Editor.Protocols.PacketModels
{
    [Serializable]
    public class ComponentState
    {
        public string type;
        public int index;
        public FieldValue[] fields;
    }
}