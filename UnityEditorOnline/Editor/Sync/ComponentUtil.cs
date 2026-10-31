using System;
using System.Collections.Generic;
using System.Text;
using UnityEditorOnline.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditorOnline.Editor.Sync
{
    public class ComponentEntry
    {
        public Component component;
        public string type;   
        public int index;     
    }

    public static class ComponentUtil
    {
        private static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        private static bool IsSynced(Component c) => c != null && !(c is SyncId);
        
        public static List<ComponentEntry> GetAll(GameObject go)
        {
            var result = new List<ComponentEntry>();
            var counts = new Dictionary<string, int>();

            foreach (Component c in go.GetComponents<Component>())
            {
                if (!IsSynced(c)) 
                    continue;

                string type = c.GetType().FullName;
                counts.TryGetValue(type, out int index);
                counts[type] = index + 1;

                result.Add(new ComponentEntry { component = c, type = type, index = index });
            }
            return result;
        }

        public static string Signature(GameObject go)
        {
            var sb = new StringBuilder();

            foreach (Component c in go.GetComponents<Component>())
            {
                if (!IsSynced(c)) 
                    continue;

                sb.Append(c.GetType().FullName).Append(';');
            }
            return sb.ToString();
        }

        public static int IndexOf(Component c)
        {
            string typeName = c.GetType().FullName;
            int index = 0;
            foreach (Component other in c.gameObject.GetComponents<Component>())
            {
                if (other == null) 
                    continue;
                if (other == c) 
                    return index;
                if (other.GetType().FullName == typeName) 
                    index++;
            }
            return 0;
        }

        public static Object Find(GameObject go, string typeName, int index)
        {
            if (go == null) 
                return null;
            if (typeName == "GameObject") 
                return go;

            int i = 0;

            foreach (Component c in go.GetComponents<Component>())
            {
                if (c == null || c.GetType().FullName != typeName) 
                    continue;
                if (i == index) 
                    return c;
                i++;
            }
            return null;
        }

        public static Type ResolveType(string fullName)
        {
            if (TypeCache.TryGetValue(fullName, out Type cached)) 
                return cached;

            Type found = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                found = asm.GetType(fullName);
                if (found != null) 
                    break;
            }
            TypeCache[fullName] = found;
            return found;
        }

    }
}