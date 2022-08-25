using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace needle.EditorPatching
{
    /// <summary>
    /// type annotated with [Harmony] attribute
    /// </summary>
    public class ManagedPatchAnnotated : ManagedPatchBase
    {
        protected override bool OnEnablePatch()
        {
            proc.Patch();
            HarmonyHelper.CollectPatches(instance, infos);
            return true;
        }

        protected override bool OnDisablePatch()
        {
            HarmonyHelper.UpdatePatchesState(instance, infos, false);
            // instance.UnpatchAll(); 
            return true;
        }

        /// <param name="method">type with HarmonyPatch attribute</param>
        internal ManagedPatchAnnotated(Type method, bool createAutomatically = true)
        {
            
            var owner = method.Name; 
            // if its a nested patch prepend with class name
            if (method.DeclaringType != null) owner = method.DeclaringType.Name + " " + owner;
            // // if its in a namespace prepend last part of namespace 
            // else if (!string.IsNullOrEmpty(type.Namespace))
            // {
            //     var ns = type.Namespace;
            //     if (ns.Contains(".")) ns = ns.Substring(ns.LastIndexOf(".", StringComparison.Ordinal));
            //     owner = ns + owner;
            // }
            Name = owner; 
            
            // mark it as a managed patch
            owner += ManagedPatchPostfix;
            instance = new Harmony(method.FullName);
            Id = instance.Id;
            Group = method.Assembly.GetGroupName();
            proc = instance.CreateClassProcessor(method);
            
            ApplyMeta(method.GetCustomAttribute<PatchMeta>());
            
            if(createAutomatically)
                OnCreated();
        }

        private readonly Harmony instance;
        private readonly PatchClassProcessor proc;
        private readonly Dictionary<MethodBase, Patches> infos = new Dictionary<MethodBase, Patches>();
    }
}