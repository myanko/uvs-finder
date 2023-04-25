using System.Collections.Generic;
using HarmonyLib;

namespace needle.EditorPatching
{
	public static class PatchInfoExtensions 
	{
		// public static void AddPatches(this PatchProcessor processor, Patches patches)
		// {
		// 	foreach (var p in patches.Prefixes)
		// 		processor.AddPrefix(new HarmonyMethod(p.));
		// }
		
		public static IEnumerable<Patch> EnumeratePatches(this Patches patches)
		{
			foreach (var e in patches.Prefixes) yield return e;
			foreach (var e in patches.Postfixes) yield return e;
			foreach (var e in patches.Transpilers) yield return e;
			foreach (var e in patches.Finalizers) yield return e;
		}
	}
}