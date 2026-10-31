using System.Globalization;

namespace UnityEditorOnline.Editor.Codec
{
    internal static class FloatList
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Join(params float[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++) 
                parts[i] = values[i].ToString("R", Inv);
            return string.Join(" ", parts);
        }

        public static float[] Parse(string v)
        {
            string[] parts = v.Split(' ');
            var result = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++) 
                result[i] = float.Parse(parts[i], Inv);
            return result;
        }
    }
}
