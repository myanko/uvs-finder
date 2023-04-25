using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEditor;
using UnityEngine;
//
namespace needle.EditorPatching
{
    public static class PatchesCollector 
     {
//         public static void CollectPatches()
//         { 
// #pragma warning disable 4014
//             InternalCollectPatches();
// #pragma warning restore 4014
//         }
//
        [InitializeOnLoadMethod]
        #if UNITY_2019_1_OR_NEWER
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        #else
		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        #endif
        internal static void CollectAll() 
        {
            // await InternalCollectPatches();
            CollectMethodsWithHarmonyAttribute();
        }

        internal static void CollectMethodsWithHarmonyAttribute() 
        {
            var methods = TypeCache.GetTypesWithAttribute<HarmonyPatch>(); 
            foreach (var m in methods)
            {
                var id = PatchHelpers.GetId(m);
                if(PatchManager.IsRegistered(id)) continue;
                var patch = new ManagedPatchAnnotated(m);
                PatchManager.RegisterPatch(patch);
            }
        }
//
//         private static bool isRunning = false;
//         private static readonly Dictionary<string, ManagedPatchIndependent> independentPatches = new Dictionary<string, ManagedPatchIndependent>();
//
//         private static async Task InternalCollectPatches()
//         {
//             if (isRunning) return;
//             isRunning = true;
//             // while (EditorApplication.isCompiling || EditorApplication.isUpdating) await Task.Delay(1);
//             // await Task.Delay(1);
//             var firstRun = true;
//             while (isRunning)
//             {
//                 if(await Task.Run(Collect)) RegisterPatches();
//                 await Task.Delay(firstRun ? 1_000 : 10_000);
//                 firstRun = false;
//             }
//             
//             void RegisterPatches()
//             {
//                 foreach (var patch in independentPatches.Values)
//                 {
//                     if(patch.Id.EndsWith(ManagedPatchBase.ManagedPatchPostfix)) continue;
//                     PatchManager.RegisterPatch(patch);
//                 }
//             }
//             
//             isRunning = false;
//         }
//
//         private static bool Collect()
//         {
//             var foundMethods = false;
//             try
//             {
//                 // TODO: check that harmony instances that have changing methods that they patch (e.g. the menu item patch) are updated properly
//                 var originalMethods = Harmony.GetAllPatchedMethods();
//                 foreach (var original in originalMethods)
//                 {
//                     // retrieve all patches
//                     var patchInfos = Harmony.GetPatchInfo(original);
//                     if (patchInfos is null) continue;
//                     foundMethods = true;
//                     foreach (var patch in patchInfos.Prefixes) Add(original, HarmonyPatchType.Prefix, patch);
//                     foreach (var patch in patchInfos.Postfixes) Add(original, HarmonyPatchType.Postfix, patch);
//                     foreach (var patch in patchInfos.Transpilers) Add(original, HarmonyPatchType.Transpiler, patch);
//                     foreach (var patch in patchInfos.Finalizers) Add(original, HarmonyPatchType.Finalizer, patch);
//                 }
//
//                 // Debug.Log("Run " + foundMethods);
//                 // break;
//             }
//             catch (InvalidOperationException)
//             {
//                 // this can happen while methods are patched or un-patched while iterating
//             }
//             return foundMethods;
//         }
//
//         private static void Add(MethodBase original, HarmonyPatchType type, Patch patch)
//         {
//             var id = PatchHelpers.GetId(original.GetType());
//             if (id.EndsWith(ManagedPatchBase.ManagedPatchPostfix)) return;
//             if (!independentPatches.ContainsKey(id)) independentPatches.Add(id, new ManagedPatchIndependent(id));
//             var prov = independentPatches[id];
//             // TODO: verify that this work if patched methods change
//             // Debug.Log(original + ", " + type + ", " + patch);
//             prov.Add(original, type, patch); 
//         }
     }
}