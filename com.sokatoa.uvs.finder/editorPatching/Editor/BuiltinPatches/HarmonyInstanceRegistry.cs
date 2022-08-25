using System;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using Mono.Cecil;
using NUnit.Framework.Internal;
using UnityEditor.Compilation;
using Debug = UnityEngine.Debug;

namespace needle.EditorPatching
{
	[PatchMeta(Description = "Patches harmony constructor to collect any form of patching via harmony. MUST be enabled before any patch is applied")]
	[HarmonyPatch]
	internal static class HarmonyInstanceRegistry
	{
		private struct State
		{
			public string Group;
		}
		
		[HarmonyPostfix]
		[HarmonyPatch(typeof(Harmony), MethodType.Constructor, typeof(string))]
		private static void Prefix(ref string id, out State __state)
		{
			var sf = new StackTrace();

			Type FindHarmonyDeclaringType(int index)
			{
				var type = sf.GetFrame(index).GetMethod().DeclaringType;
				if (type == typeof(Harmony) || type == typeof(HarmonyInstanceRegistry)) return FindHarmonyDeclaringType(++index);
				return type; 
			}

			var caller = FindHarmonyDeclaringType(0);
			__state = new State();
			if (caller != null)
			{
				// state is group
				__state.Group = caller.Assembly.GetGroupName();
				if (!id.Contains("."))
				{
					if (caller.IsGenericType)
					{
						id = string.Join("-", caller.GenericTypeArguments.Select(t => t.Name));
					}
					else
					{
						id = caller.ToString();
					}
				}
			}
			else __state.Group = id;
		}

		[HarmonyPostfix]
		[HarmonyPatch(typeof(Harmony), MethodType.Constructor, typeof(string))]
		private static void Postfix(Harmony __instance, State __state) 
		{
			var id = __instance.Id;
			var registered = PatchManager.IsRegistered(id);
			if(PatchManager.AllowDebugLogs)
				Debug.Log("New instance: " + id + ", " + __instance.Id + ", is registered? " + registered);
			if (!registered)
			{
				var i = new HarmonyInstancePatch(__instance, id, __state.Group);
				PatchManager.RegisterPatch(i);
			}
		}
	}
}