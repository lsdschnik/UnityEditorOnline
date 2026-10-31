using System;
using UnityEngine;

namespace UnityEditorOnline.Editor.Protocols.Packets
{
    [Serializable]
    public class CameraPacket
    {
        public string user;
        public Color color;
        public Vector3 position;
        public Quaternion rotation;
        public bool left;
    }
}
