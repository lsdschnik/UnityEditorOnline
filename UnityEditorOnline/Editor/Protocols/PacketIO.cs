using UnityEngine;

namespace UnityEditorOnline.Editor.Protocols
{
    public static class PacketIO
    {
        public static string Wrap<T>(string type, T packet)
        {
            return JsonUtility.ToJson(new Envelope { type = type, body = JsonUtility.ToJson(packet) });
        }

        public static string Wrap(string type)
        {
            return JsonUtility.ToJson(new Envelope { type = type, body = string.Empty });
        }

    }
}