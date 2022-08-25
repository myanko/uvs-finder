// using System.Collections.Generic;
// using System.Reflection;
// using HarmonyLib;
// using UnityEngine;
//
// namespace needle.EditorPatching
// {
//     /// <summary>
//     /// a patch that has not used annotation and is not registered but just applied via patch all and a own harmony instance
//     /// </summary>
//     public class ManagedPatchIndependent : ManagedPatchBase
//     {
//         public readonly Dictionary<MethodBase, Dictionary<HarmonyPatchType, List<Patch>>> Patches = new Dictionary<MethodBase, Dictionary<HarmonyPatchType, List<Patch>>>();
//
//         private string currentId;
//         private readonly Harmony Instance;
//
//         protected override bool OnEnablePatch()
//         {
//             foreach (var patches in Patches)
//             {
//                 var original = patches.Key;
//                 var dict = patches.Value;
//                 var prefixes = dict.ContainsKey(HarmonyPatchType.Prefix) ? dict[HarmonyPatchType.Prefix] : null;
//                 var postfixes = dict.ContainsKey(HarmonyPatchType.Postfix) ? dict[HarmonyPatchType.Postfix] : null;
//                 var tps = dict.ContainsKey(HarmonyPatchType.Transpiler) ? dict[HarmonyPatchType.Transpiler] : null;
//                 var finalizers = dict.ContainsKey(HarmonyPatchType.Finalizer) ? dict[HarmonyPatchType.Finalizer] : null;
//                 
//                 var maxCount = GetMaxCount(prefixes, postfixes, tps, finalizers);
//                 for (var i = 0; i < maxCount; i++)
//                 {
//                     var prefix = GetMethod(i, prefixes);
//                     var postfix = GetMethod(i, postfixes);
//                     var transpiler = GetMethod(i, tps);
//                     var finalizer = GetMethod(i, finalizers);
//                     Instance.Patch(original, prefix, postfix, transpiler, finalizer);
//                 }
//             }
//
//             return true;
//         }
//
//         protected override bool OnDisablePatch()
//         {
//             foreach (var method in Patches.Keys)
//             {
//                 Instance.Unpatch(method, HarmonyPatchType.All, currentId);
//             }
//
//             currentId = Instance.Id;
//             return true;
//         }
//         
//         public ManagedPatchIndependent(string id, string group, bool isActive = true)
//         {
//             this.Name = id;
//             this.currentId = id;
//             this.Id = id;
//             this.IsActive = isActive;
//             this.Instance = new Harmony(id + "." + ManagedPatchPostfix); 
//             OnCreated(); 
//         }
//
//         public void Add(MethodBase original, HarmonyPatchType type, Patch patch)
//         {
//             if (original == null) return;
//             if (patch == null) return;
//             if(!Patches.ContainsKey(original)) Patches.Add(original, new Dictionary<HarmonyPatchType, List<Patch>>());
//             var dict = Patches[original];
//             if(!dict.ContainsKey(type)) dict.Add(type, new List<Patch>());
//             if(!dict[type].Contains(patch))
//                 dict[type].Add(patch);
//         }
//
//         private static HarmonyMethod GetMethod(int index, IReadOnlyList<Patch> patches)
//         {
//             return patches != null && patches.Count > index ? new HarmonyMethod(patches[index].PatchMethod) : null;
//         }
//
//         private static int GetMaxCount(params List<Patch>[] lists)
//         {
//             var mc = 0;
//             foreach (var e in lists)
//             {
//                 if (e == null) continue;
//                 mc = Mathf.Max(mc, e.Count);
//             }
//             return mc;
//         }
//     }
// }