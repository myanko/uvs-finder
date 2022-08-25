using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEditor;
using UnityEngine;

namespace needle.EditorPatching
{
	/// <summary>
	/// instantiated from harmony constructor - used when some other script totally bypasses patch management
	/// when the harmony constructor patch runs it grabs the handle and registers that instance
	/// and can now enable/disable patches from here
	/// </summary>
	internal class HarmonyInstancePatch : ManagedPatchBase
	{
		protected override bool OnEnablePatch()
		{
			HarmonyHelper.UpdatePatchesState(harmonyInstance, infos, true);
			return true;
		}

		protected override bool OnDisablePatch()
		{
			CollectPatches();
			HarmonyHelper.UpdatePatchesState(harmonyInstance, infos, false);
			return true;
		}

		private void CollectPatches()
		{
			if (HarmonyHelper.CollectPatches(harmonyInstance, infos))
			{
				IsActive = true;
			}
		}

		private readonly Harmony harmonyInstance;
		private readonly Dictionary<MethodBase, Patches> infos = new Dictionary<MethodBase, Patches>();

		public HarmonyInstancePatch(Harmony harmony, string name, string group)
		{
			harmonyInstance = harmony;
			this.Id = harmony.Id;
			this.Name = name;
			Group = group;

			var counter = 0;
			var waitTime = 60;
			var collectCounter = 0;

			void OnUpdate()
			{
				++counter;
				if (counter % waitTime == 0)
				{
					if (EditorUtils.ProjectSettingsOpenAndFocused())
					{
						waitTime *= 2;
						waitTime = Mathf.Clamp(waitTime, 1, 120);
						CollectPatches();
						collectCounter += 1;
						if (collectCounter > 5)
						{
							EditorApplication.update -= OnUpdate;
						}
					}
				}
			}

			EditorApplication.update += OnUpdate;
		}
	}
}