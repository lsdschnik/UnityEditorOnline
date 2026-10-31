using System;
using System.Globalization;
using UnityEngine;

namespace UnityEditorOnline.Editor.Codec
{
    // Кривая: "&lt;preWrap&gt;|&lt;postWrap&gt;|&lt;ключ&gt;;&lt;ключ&gt;;..."
    // Ключ:   "time value inTangent outTangent weightedMode inWeight outWeight"
    internal static class CurveCodec
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Encode(AnimationCurve curve)
        {
            if (curve == null) 
                return "0|0|";

            var keys = new string[curve.length];
            for (int i = 0; i < curve.length; i++)
            {
                Keyframe k = curve[i];
                keys[i] = FloatList.Join(k.time, k.value, k.inTangent, k.outTangent)
                          + " " + ((int)k.weightedMode).ToString(Inv)
                          + " " + FloatList.Join(k.inWeight, k.outWeight);
            }

            return ((int)curve.preWrapMode).ToString(Inv) + "|"
                 + ((int)curve.postWrapMode).ToString(Inv) + "|"
                 + string.Join(";", keys);
        }

        public static AnimationCurve Decode(string v)
        {
            string[] a = v.Split('|');
            if (a.Length != 3) 
                throw new FormatException("bad curve");

            var curve = new AnimationCurve();

            if (a[2].Length > 0)
            {
                foreach (string keyStr in a[2].Split(';'))
                {
                    string[] f = keyStr.Split(' ');
                    if (f.Length != 7) 
                        throw new FormatException("bad keyframe");

                    var k = new Keyframe(
                        float.Parse(f[0], Inv), float.Parse(f[1], Inv),
                        float.Parse(f[2], Inv), float.Parse(f[3], Inv),
                        float.Parse(f[5], Inv), float.Parse(f[6], Inv));

                    k.weightedMode = (WeightedMode)int.Parse(f[4], Inv);
                    curve.AddKey(k);
                }
            }

            curve.preWrapMode = (WrapMode)int.Parse(a[0], Inv);
            curve.postWrapMode = (WrapMode)int.Parse(a[1], Inv);
            return curve;
        }
    }
}