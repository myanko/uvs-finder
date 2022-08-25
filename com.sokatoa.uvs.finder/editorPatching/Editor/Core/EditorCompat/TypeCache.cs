#if !UNITY_2019_2_OR_NEWER

using System;
using System.Linq;

namespace needle.EditorPatching
{
    public static class TypeCache
    {
        public static Type[] GetTypesWithAttribute<T>() where T : Attribute
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetLoadableTypes())
                .SelectMany(t => t)
                .Where(t => t.GetCustomAttributes(typeof(T), true)?.Length > 0)
                .ToArray();
        }

        public static Type[] GetTypesDerivedFrom(Type patchType)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetLoadableTypes())
                .SelectMany(t => t)
                .Where(patchType.IsAssignableFrom)
                .ToArray();
        }
    }
}

#endif