using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local

namespace needle.EditorPatching
{
	public class HarmonyUnityDebugLogPatch : EditorPatchProvider
	{
		public override string DisplayName => "Harmony Log To Unity Console";
		public override string Description => "When on and EditorPatching Debug Log is enabled harmony file logs will be logged to the Unity Console";

		protected override void OnGetPatches(List<EditorPatch> patches)
		{
			patches.Add(new LogPatch());
			patches.Add(new FlushBufferPatch());
		}

		private class LogPatch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				targetMethods.Add(typeof(FileLog)?.GetMethod(nameof(FileLog.Log)));
				return Task.CompletedTask;
			}

			private static bool Prefix(string str)
			{
				if (string.IsNullOrWhiteSpace(str)) return false;
				while (str.StartsWith("#")) str = str.Substring(1);
				str = str.TrimStart();
				#if UNITY_2019_1_OR_NEWER
				Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, str);
				#else
				Debug.Log(str);
				#endif
				return true;
			}
		}

		private class FlushBufferPatch : EditorPatch
		{
			protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
			{
				targetMethods.Add(typeof(FileLog)?.GetMethod(nameof(FileLog.FlushBuffer)));
				return Task.CompletedTask;
			}

			private static bool Prefix(List<string> ___buffer)
			{
				var str = string.Join("\n", ___buffer);
				if (string.IsNullOrWhiteSpace(str)) return true;
				try
				{				
					#if UNITY_2019_1_OR_NEWER
					Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, str);
					#else
					Debug.Log(str);
					#endif
				}
				catch (FormatException)
				{
					Debug.Log(str);
				}
				return true;
			}
		}
	}
}