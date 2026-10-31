using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditorOnline.Editor.Protocols.PacketModels;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Codec
{
    public enum DecodeResult { Ok, Unsupported, Unresolved }
    public static class PropertyCodec
    {
        private static readonly string[] GameObjectFields = { "m_Name", "m_IsActive", "m_Layer", "m_TagString", "m_StaticEditorFlags" };

        private static readonly HashSet<string> SkipPaths = new HashSet<string>
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Script", "m_EditorHideFlags", "m_EditorClassIdentifier",
            "m_Father", "m_Children", "m_RootOrder", "m_LocalEulerAnglesHint"
        };

        private class PendingRef 
        { 
            public Object target; 
            public string path; 
            public string value; 
        }

        private static readonly List<PendingRef> Pending = new List<PendingRef>();

        public static bool TryEncode(SerializedProperty p, out string v)
        {
            v = null;
            switch (p.propertyType)
            {
                case SerializedPropertyType.Integer:
                    v = p.longValue.ToString(CultureInfo.InvariantCulture); return true;
                case SerializedPropertyType.Boolean:
                    v = p.boolValue ? "1" : "0"; return true;
                case SerializedPropertyType.Float:
                    v = p.type == "double" ? p.doubleValue.ToString("R", CultureInfo.InvariantCulture) : p.floatValue.ToString("R", CultureInfo.InvariantCulture);
                    return true;
                case SerializedPropertyType.String:
                    v = p.stringValue ?? ""; return true;
                case SerializedPropertyType.Color:
                    { Color c = p.colorValue; v = FloatList.Join(c.r, c.g, c.b, c.a); return true; }
                case SerializedPropertyType.Vector2:
                    { Vector2 x = p.vector2Value; v = FloatList.Join(x.x, x.y); return true; }
                case SerializedPropertyType.Vector3:
                    { Vector3 x = p.vector3Value; v = FloatList.Join(x.x, x.y, x.z); return true; }
                case SerializedPropertyType.Vector4:
                    { Vector4 x = p.vector4Value; v = FloatList.Join(x.x, x.y, x.z, x.w); return true; }
                case SerializedPropertyType.Quaternion:
                    { Quaternion x = p.quaternionValue; v = FloatList.Join(x.x, x.y, x.z, x.w); return true; }
                case SerializedPropertyType.Vector2Int:
                    { Vector2Int x = p.vector2IntValue; v = FloatList.Join(x.x, x.y); return true; }
                case SerializedPropertyType.Vector3Int:
                    { Vector3Int x = p.vector3IntValue; v = FloatList.Join(x.x, x.y, x.z); return true; }
                case SerializedPropertyType.Enum:
                    if (p.enumValueIndex < 0) return false;
                    v = p.enumValueIndex.ToString(CultureInfo.InvariantCulture); return true;
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Character:
                case SerializedPropertyType.ArraySize:
                    v = p.intValue.ToString(CultureInfo.InvariantCulture); return true;
                case SerializedPropertyType.ObjectReference:
                    v = RefCodec.Encode(p.objectReferenceValue); return true;
                case SerializedPropertyType.AnimationCurve:
                    v = CurveCodec.Encode(p.animationCurveValue); return true;
                default:
                    return false; 
            }
        }

        public static DecodeResult TryDecode(SerializedProperty p, string v)
        {
            try
            {
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer: p.longValue = long.Parse(v, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.Boolean: p.boolValue = v == "1"; break;
                    case SerializedPropertyType.Float:
                        if (p.type == "double") p.doubleValue = double.Parse(v, CultureInfo.InvariantCulture);
                        else p.floatValue = float.Parse(v, CultureInfo.InvariantCulture);
                        break;
                    case SerializedPropertyType.String: p.stringValue = v; break;
                    case SerializedPropertyType.Color:
                        { float[] f = FloatList.Parse(v); p.colorValue = new Color(f[0], f[1], f[2], f[3]); break; }
                    case SerializedPropertyType.Vector2:
                        { float[] f = FloatList.Parse(v); p.vector2Value = new Vector2(f[0], f[1]); break; }
                    case SerializedPropertyType.Vector3:
                        { float[] f = FloatList.Parse(v); p.vector3Value = new Vector3(f[0], f[1], f[2]); break; }
                    case SerializedPropertyType.Vector4:
                        { float[] f = FloatList.Parse(v); p.vector4Value = new Vector4(f[0], f[1], f[2], f[3]); break; }
                    case SerializedPropertyType.Quaternion:
                        { float[] f = FloatList.Parse(v); p.quaternionValue = new Quaternion(f[0], f[1], f[2], f[3]); break; }
                    case SerializedPropertyType.Vector2Int:
                        { float[] f = FloatList.Parse(v); p.vector2IntValue = new Vector2Int((int)f[0], (int)f[1]); break; }
                    case SerializedPropertyType.Vector3Int:
                        { float[] f = FloatList.Parse(v); p.vector3IntValue = new Vector3Int((int)f[0], (int)f[1], (int)f[2]); break; }
                    case SerializedPropertyType.Enum: p.enumValueIndex = int.Parse(v, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.ArraySize:
                        p.intValue = int.Parse(v, CultureInfo.InvariantCulture); break;
                    case SerializedPropertyType.ObjectReference:
                        return RefCodec.Decode(p, v);
                    case SerializedPropertyType.AnimationCurve:
                        p.animationCurveValue = CurveCodec.Decode(v); break;
                    default:
                        return DecodeResult.Unsupported;
                }
                return DecodeResult.Ok;
            }
            catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException)
            {
                return DecodeResult.Unsupported;
            }
        }
        public static FieldValue[] Snapshot(Object target, bool isGameObject)
        {
            var list = new List<FieldValue>();
            var so = new SerializedObject(target);

            if (isGameObject)
            {
                foreach (string path in GameObjectFields)
                {
                    SerializedProperty p = so.FindProperty(path);
                    if (p != null && TryEncode(p, out string v)) list.Add(new FieldValue { path = path, value = v });
                }
                return list.ToArray();
            }

            SerializedProperty it = so.GetIterator();
            bool enter = true;
            while (it.Next(enter))
            {
                enter = true;

                if (SkipPaths.Contains(it.propertyPath)) 
                { 
                    enter = false; 
                    continue; 
                }

                if (it.propertyType == SerializedPropertyType.Generic) 
                    continue; 

                if (TryEncode(it, out string v))
                    list.Add(new FieldValue { path = it.propertyPath, value = v });

                enter = false;
            }
            return list.ToArray();
        }

        public static void Apply(Object target, FieldValue[] fields)
        {
            if (target == null || fields == null) 
                return;

            var so = new SerializedObject(target);

            foreach (FieldValue f in fields)
            {
                SerializedProperty p = so.FindProperty(f.path);

                if (p != null && TryDecode(p, f.value) == DecodeResult.Unresolved)
                {
                    if (Pending.Count > 1000) Pending.RemoveAt(0);
                    Pending.Add(new PendingRef { target = target, path = f.path, value = f.value });
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void RetryPending()
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                PendingRef pr = Pending[i];
                if (pr.target == null) 
                { 
                    Pending.RemoveAt(i); 
                    continue; 
                }

                var so = new SerializedObject(pr.target);
                SerializedProperty p = so.FindProperty(pr.path);
                if (p == null) 
                { 
                    Pending.RemoveAt(i); 
                    continue; 
                }

                if (TryDecode(p, pr.value) != DecodeResult.Unresolved)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    Pending.RemoveAt(i);
                }
            }
        }

    }
}