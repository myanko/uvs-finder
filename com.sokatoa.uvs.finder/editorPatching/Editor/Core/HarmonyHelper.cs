using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace needle.EditorPatching
{
	internal static class HarmonyHelper
	{
		public static bool CollectPatches(Harmony instance, Dictionary<MethodBase, Patches> infos, Action<MethodBase> foundNew = null)
		{
			var pm = instance.GetPatchedMethods();
			var anyAdded = false;
			foreach (var p in pm)
			{
				if (!infos.ContainsKey(p))
				{
					var patchInfos = Harmony.GetPatchInfo(p);
					if (patchInfos.Owners.Contains(instance.Id))
					{
						infos.Add(p, patchInfos);
						anyAdded = true;
						foundNew?.Invoke(p);
					}
				}
			}

			return anyAdded;
		}
		
		public static void UpdatePatchesState(Harmony instance, Dictionary<MethodBase, Patches> infos, bool active)
		{
			// Debug.Log("Enable " + patchedMethods.Count() + " " + Id);
			if (infos != null)
			{
				foreach (var kvp in infos)
				{
					var p = kvp.Key;
					var patches = kvp.Value;
					for (var i = 0; i < patches.Owners.Count; i++)
					{
						var owner = patches.Owners[i];
						if (owner == instance.Id)
						{
							if (active)
							{
								instance.Patch(p,
									TryGetPatch(i, patches.Prefixes),
									TryGetPatch(i, patches.Postfixes),
									TryGetPatch(i, patches.Transpilers),
									TryGetPatch(i, patches.Finalizers)
								);
							}
							else
							{
								instance.Unpatch(p, HarmonyPatchType.All, instance.Id);
							}
						}
					} 
				}
			}
		}

		public static HarmonyMethod TryGetPatch(int index, IReadOnlyList<Patch> patches)
		{
			if (index >= 0 && index < patches.Count)
				return new HarmonyMethod(patches[index].PatchMethod);
			return null;
		}
	}
}