using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorOnline.Editor.Sync;
using UnityEditorOnline.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Codec
{
    // Ссылки на объекты.
    // Ссылка на объект сцены:  "s|&lt;guid&gt;|&lt;тип компонента&gt;|&lt;индекс&gt;"
    // Ссылка на ассет:         "a|&lt;guid ассета&gt;|&lt;local file id&gt;"
    // Пусто:                   "n"
    internal static class RefCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static readonly Dictionary<string, Object> AssetCache = new Dictionary<string, Object>();

        public static string Encode(Object o)
        {
            if (o == null) return "n";

            Component comp = o as Component;
            GameObject go = comp != null ? comp.gameObject : o as GameObject;

            if (go != null && go.scene.IsValid())
            {
                SyncId id = go.GetComponent<SyncId>();
                if (id == null || string.IsNullOrEmpty(id.Guid)) return "n";
                string type = comp != null ? comp.GetType().FullName : "GameObject";
                int index = comp != null ? ComponentUtil.IndexOf(comp) : 0;
                return "s|" + id.Guid + "|" + type + "|" + index.ToString(Inv);
            }

            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long fileId))
                return "a|" + guid + "|" + fileId.ToString(Inv);

            return "n";
        }

        public static DecodeResult Decode(SerializedProperty p, string v)
        {
            if (v == "n")
            {
                p.objectReferenceValue = null;
                return DecodeResult.Ok;
            }

            string[] a = v.Split('|');

            if (a[0] == "s" && a.Length == 4)
            {
                GameObject go = SyncRegistry.Find(a[1]);
                Object target = ComponentUtil.Find(go, a[2], int.Parse(a[3], Inv));
                if (target == null) return DecodeResult.Unresolved;
                p.objectReferenceValue = target;
                return DecodeResult.Ok;
            }

            if (a[0] == "a" && a.Length == 3)
            {
                Object asset = LoadAsset(a[1], long.Parse(a[2], Inv));
                if (asset == null) return DecodeResult.Unresolved;
                p.objectReferenceValue = asset;
                return DecodeResult.Ok;
            }

            return DecodeResult.Unsupported;
        }

        private static Object LoadAsset(string guid, long fileId)
        {
            string key = guid + "|" + fileId.ToString(Inv);
            if (AssetCache.TryGetValue(key, out Object cached) && cached != null) return cached;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) 
                return null; 
            foreach (Object a in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (a != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(a, out string _, out long id) && id == fileId)
                {
                    AssetCache[key] = a;
                    return a;
                }
            }
            return null;
        }
    }
}
