using System;
using System.Collections.Generic;

namespace needle.EditorPatching
{
    [AttributeUsage(AttributeTargets.Class)]
    public class PatchMeta : Attribute
    {
        public string Description;
        public List<string> Category;
        public List<string> Tags;
        public List<string> UnsupportedEditors;

        public Type CanEnableCallbackType;
        public string CanEnableCallbackMethod;
    }
}