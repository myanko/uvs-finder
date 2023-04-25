using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using needle.EditorPatching;
using UnityEditor;
using UnityEngine;

namespace Needle
{
	internal static class EarlyInit
	{
		static EarlyInit()
		{
			Debug.Log("Early init patching");
			PatchManager.Init();
		}
	}
}

namespace needle.EditorPatching
{
	// [assembly:InternalsVisibleTo("UnityEngine.UI")]
	
	internal struct EditorPatchProviderInfo
	{
		public string PatchID;
		public EditorPatchProvider Instance;
		public IReadOnlyList<EditorPatchData> Data;

		public override string ToString()
		{
			return PatchID + ", Patches: " + Data.Count;
		}
	}

	internal class EditorPatchData
	{
		public EditorPatch EditorPatch;
		public MethodInfo PrefixMethod;
		public MethodInfo PostfixMethod;
		public MethodInfo TranspilerMethod;
		public MethodInfo FinalizerMethod;
		
		public List<MethodBase> PatchedMethods;
	}
	
	// TODO: expose HarmonyInstance.DEBUG = true -> https://github.com/pardeike/Harmony/issues/79#issuecomment-386356598

	[InitializeOnLoad]
	public static class PatchManager
	{
		[MenuItem(Constants.MenuItem + "Clear Settings Cache", priority = Constants.DefaultTopLevelPriority)]
		public static void ClearSettingsCache()
		{
			PatchManagerSettings.Clear(true);
		}


		[MenuItem("Help/EditorPatching/Disable All Patches")]
		[MenuItem(Constants.MenuItem + "Disable All Patches", priority = Constants.DefaultTopLevelPriority)]
		public static void DisableAllPatches()
		{
			DisableAllPatches(true);
		}

		public static void DisableAllPatches(bool resetPersistence)
		{
			if(AllowDebugLogs)
				Debug.Log("DISABLE ALL PATCHES");
			foreach (var prov in patchProviders)
			{
				var instance = prov.Value.Instance;
				if (instance.GetIsActive())
				{
					DisablePatch(instance, false);
					if (resetPersistence) continue;
					// keep persistence state
					PatchManagerSettings.SetPersistentActive(instance.ID(), true);
				}
			}

			foreach (var man in KnownPatches)
			{
				if (man.IsActive)
				{
					man.DisablePatch();
					if (resetPersistence) continue;
					// keep persistence state
					PatchManagerSettings.SetPersistentActive(man.Id, true); 
				}
			}
		}

		public static bool IsInitialized => _isInitialized;

		private static bool _isInitialized;

		
		static PatchManager()
		{
			Init();
		}
		
		internal static void Init()
		{
			if (_isInitialized) return;
			_isInitialized = true;
			if(AllowDebugLogs)
				Debug.Log("Init patch manager");

			PatchesCollector.CollectMethodsWithHarmonyAttribute();

			if (!IsPersistentDisabled(typeof(HarmonyInstanceRegistry).FullName))
			{
				var t = EnablePatch(typeof(HarmonyInstanceRegistry).FullName);
				if(!t.IsCompleted) t.RunSynchronously();
			}
			
			// PatchesCollector.CollectPatches();
			// PatchesCollector.CollectAll();

			var patchType = typeof(EditorPatchProvider);
			var patches = TypeCache.GetTypesDerivedFrom(patchType);
			foreach (var patch in patches)
			{
				if (patch.IsAbstract) continue;
				if (patch.GetCustomAttribute<NoAutoDiscover>() != null) continue;
				RegisterPatch(FormatterServices.GetUninitializedObject(patch) as EditorPatchProvider);
			}

			DelayedEnablePatchedThatHadBeenActivePreviously();

			// AssemblyReloadEvents.beforeAssemblyReload += DisableAllPatches(false);
			// CompilationPipeline.compilationStarted += o => DisableAllPatches(false);

			EditorApplication.update += OnEditorUpdate;
		}

		private static int patchesChangedFrame;
		private static int threadedFrameCount;

		private static void OnEditorUpdate()
		{
			if (!EditorApplication.isPlaying && patchesChangedFrame > 0 && threadedFrameCount - patchesChangedFrame > 2)
			{
				patchesChangedFrame = -1;
				// InternalEditorUtility.RepaintAllViews();
				// Debug.Log("Repaint");
			}
			++threadedFrameCount;
		}

		// this is also called from background thread
		private static void InternalMarkChanged() => patchesChangedFrame = threadedFrameCount;

		private static void DelayedEnablePatchedThatHadBeenActivePreviously()
		{
			if (!AutomaticallyEnablePersistentPatches) return;
			foreach (var patch in registeredPatchProviders)
			{
				if (PatchManagerSettings.PersistentActive(patch))
					EnablePatch(patch);
			}
		}

		private static readonly HashSet<EditorPatchProvider> registeredPatchProviders = new HashSet<EditorPatchProvider>();
		private static readonly Dictionary<string, EditorPatchProviderInfo> patchProviders = new Dictionary<string, EditorPatchProviderInfo>();
		private static readonly Dictionary<string, Harmony> harmonyPatches = new Dictionary<string, Harmony>();

		/// <summary>
		/// harmony instances (owners) that are not using patch providers
		/// </summary>
		private static readonly Dictionary<string, IManagedPatch> knownPatches = new Dictionary<string, IManagedPatch>();

		/// <summary>
		/// key is patch id, value is patch file path
		/// </summary>
		private static readonly Dictionary<string, string> filePaths = new Dictionary<string, string>();

		public static bool AutomaticallyEnablePersistentPatches = true;

		public static bool AllowDebugLogs
		{
			get => PatchManagerSettings.instance.DebugLog;
			set => PatchManagerSettings.instance.DebugLog = value;
		}

		public static IReadOnlyCollection<EditorPatchProvider> RegisteredPatchProviders => registeredPatchProviders;
		public static IReadOnlyCollection<IManagedPatch> KnownPatches => knownPatches.Values;

		public static bool IsActive(string id)
		{
			if (patchProviders.ContainsKey(id)) return patchProviders[id].Instance.GetIsActive();
			if (knownPatches.ContainsKey(id)) return knownPatches[id].IsActive;
			return false;
		}

		public static bool IsPersistentEnabled(string id) => PatchManagerSettings.PersistentActive(id);		
		public static bool IsPersistentDisabled(string id) => PatchManagerSettings.PersistentInactive(id);
		public static bool HasPersistentSetting(string id) => PatchManagerSettings.HasPersistentSetting(id);

		public static bool TryGetFilePathForPatch(EditorPatchProvider prov, out string path) => TryGetFilePathForPatch(prov?.ID(), out path);

		public static bool TryGetFilePathForPatch(string id, out string path)
		{
			if (string.IsNullOrEmpty(id))
			{
				path = null;
				return false;
			}
            
			if (!filePaths.ContainsKey(id) && patchProviders.ContainsKey(id))
			{
				var prov = patchProviders[id].Instance;
				path = AssetDatabase.FindAssets(prov.GetType().Name).Select(AssetDatabase.GUIDToAssetPath)
					.FirstOrDefault(f => f != null && f.EndsWith($".cs"));
				filePaths.Add(id, path);
			}

			if (filePaths.ContainsKey(id))
			{
				path = filePaths[id];
				return File.Exists(path);
			}

			path = null;
			return false;
		}

		public static bool PatchIsActive(EditorPatchProvider patchProvider)
		{
			return PatchIsActive(patchProvider.ID());
		}

		public static bool PatchIsActive(string id) => harmonyPatches.ContainsKey(id);

		public static bool IsRegistered(EditorPatchProvider provider)
		{
			if (provider == null) return false;
			var type = provider.GetType();
			return type.FullName != null && patchProviders.ContainsKey(type.FullName);
		}

		public static bool IsRegistered(string id) => knownPatches.ContainsKey(id);
        
		public static void UnregisterAndDisablePatch(EditorPatchProvider patchProvider)
		{
			var id = patchProvider?.ID();
			if (id == null || id.Length <= 0) return;
			if (registeredPatchProviders.Contains(patchProvider))
			{
				patchProvider.DisablePatch();
				registeredPatchProviders.Remove(patchProvider);
			}
			if (knownPatches.ContainsKey(id))
			{
				knownPatches[id].DisablePatch();
				knownPatches.Remove(id);
			}
		}

		public static void RegisterPatch(IManagedPatch patch)
		{
			var id = patch.Id;
			if (patchProviders.ContainsKey(id)) return;
			if (knownPatches.ContainsKey(id)) return;
			knownPatches.Add(id, patch);
			if (!PatchManagerSettings.PersistentActive(id)) patch.DisablePatch();
		}
        
		public static void RegisterPatch(EditorPatchProvider patchProvider, bool allowUpdate = false)
		{
			var id = patchProvider?.ID();

			// avoid registering patches multiple times
			if (id == null) return;
			if (!allowUpdate && patchProviders.ContainsKey(id)) return;
            
			if (string.IsNullOrEmpty(id))
			{
				// ReSharper disable once SuspiciousTypeConversion.Global
				Debug.LogWarning("A patch has no ID " + patchProvider + " and will be ignored");
				return;
			}

			var patches = patchProvider.GetPatches();
			var patchData = new List<EditorPatchData>();
			const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
			foreach (var patch in patches)
			{
				var type = patch.GetType();
				// cache the static patch methods
				var prefix = type.GetMethod("Prefix", flags);
				var postfix = type.GetMethod("Postfix", flags);
				var transpiler = type.GetMethod("Transpiler", flags);
				var finalizer = type.GetMethod("Finalizer", flags);
				patchData.Add(new EditorPatchData()
				{
					EditorPatch = patch,
					PrefixMethod = prefix,
					PostfixMethod = postfix,
					TranspilerMethod = transpiler,
					FinalizerMethod = finalizer
				});
			}

			// TBD: Do we want to automatically remove and possibly warn if methods are null?

			var info = new EditorPatchProviderInfo()
			{
				PatchID = id,
				Instance = patchProvider,
				Data = patchData
			};

			if (allowUpdate && patchProviders.ContainsKey(id))
			{
				var prev = patchProviders[id];
				registeredPatchProviders.RemoveWhere(e => e.Id == prev.PatchID);
				knownPatches.Remove(prev.PatchID);
				patchProviders[id] = info;
			}
			else 
				patchProviders.Add(id, info);
            
			registeredPatchProviders.Add(patchProvider);
			knownPatches.Add(id, patchProvider);
			patchProvider.OnRegistered();
		}

		public static bool SuppressAllExceptions = false;

		public static Task<bool> EnablePatch(EditorPatchProvider patchProvider, bool enablePersistent = true)
		{
			var patchType = patchProvider.ID();
			return EnablePatch(patchType, enablePersistent);
		}

		public static Task<bool> EnablePatch(Type patchType, bool enablePersistent = true)
		{
			var patchID = patchType.FullName;
			if (patchID == null) return CompletedTaskFailed;
			var t = EnablePatch(patchID, enablePersistent);
			return t;
		}

		public static Task<bool> EnablePatch(string patchID, bool enablePersistent = true)
		{ 
			SetHarmonyDebugState(AllowDebugLogs);

			Task<bool> task = null;
			var wantEnable = true;
			
			if (patchProviders.ContainsKey(patchID) && !harmonyPatches.ContainsKey(patchID))
			{
				var info = patchProviders[patchID];
				var patchData = info.Data;
				if (patchData.Count <= 0)
				{
					if (AllowDebugLogs) Debug.LogWarning("Patch " + patchID + " did not return any methods");
				}

				wantEnable &= patchProviders[patchID].Instance.OnWillEnablePatch();
				if (wantEnable)
				{
					var instance = new Harmony(patchID);
					task = ApplyPatch(instance, info, WaitingForActivation);
					harmonyPatches.Add(patchID, instance);
					patchProviders[patchID].Instance.OnEnabledPatch();
					if(enablePersistent && patchProviders[patchID].Instance.Persistent())
						PatchManagerSettings.SetPersistentActive(patchID, true);
					InternalMarkChanged();
				} 
				else if(AllowDebugLogs)
					Debug.Log(patchID + " does not want to be enabled. Returned false in " + nameof(EditorPatchProvider.OnWillEnablePatch));
			}
			else
			{
				if(AllowDebugLogs && !knownPatches.ContainsKey(patchID))
					Debug.LogWarning("Can not enable " + patchID + ": Patch is unknown");
			}

			if (wantEnable && knownPatches.ContainsKey(patchID))
			{
				var patch = knownPatches[patchID];
				if (!patch.IsActive)
				{
					patch.EnablePatch(patchID.EndsWith(nameof(HarmonyInstanceRegistry)));
					if(enablePersistent)
						PatchManagerSettings.SetPersistentActive(patchID, true);
					InternalMarkChanged();
				}
			}
			

			return task ?? CompletedTaskFailed;
		}

		public static Task DisablePatch(EditorPatchProvider patch, bool fast = false, bool setPersistentState = true)
		{
			if (patch == null) return Task.CompletedTask;
			return DisablePatch(patch.ID(), fast, setPersistentState);
		}

		public static Task DisablePatch(Type patch, bool fast = false, bool setPersistentState = true)
		{
			var patchID = patch.FullName;
			if (patchID == null) return Task.CompletedTask;
			return DisablePatch(patchID, fast, setPersistentState);
		}

		public static Task DisablePatch(string patchID, bool fast = false, bool setPersistentState = true)
		{
			var task = Task.CompletedTask;
			
			SetHarmonyDebugState(AllowDebugLogs);
			
			if (WaitingForActivation.Contains(patchID)) WaitingForActivation.Remove(patchID);

			if (patchProviders.ContainsKey(patchID) && harmonyPatches.ContainsKey(patchID))
			{
				var instance = harmonyPatches[patchID];
				var prov = patchProviders[patchID];

				if (fast)
				{
					var taskList = new List<Task> {task};
					foreach (var e in prov.Data)
					{
						if (e.PatchedMethods == null) continue;
						foreach (var original in e.PatchedMethods)
						{
							var t = UnpatchFast(original, instance);
							taskList.Add(t);
						}
						e.PatchedMethods.Clear();
					}
					task = Task.WhenAll(taskList);
				}
				else
				{
					// clear cache first to not collect duplicates
					foreach (var e in prov.Data) e.PatchedMethods?.Clear();
					instance.UnpatchAll(patchID);
					if (AllowDebugLogs) Debug.Log("Unpatched " + patchID);
				}
				
				harmonyPatches.Remove(patchID);
				prov.Instance.OnDisabledPatch();
				InternalMarkChanged();
				if(setPersistentState)
					PatchManagerSettings.SetPersistentActive(patchID, false);
			}
            
			if (knownPatches.ContainsKey(patchID))
			{
				var patch = knownPatches[patchID];
				if (patch.IsActive)
				{
					patch.DisablePatch();
					InternalMarkChanged();
					if(setPersistentState)
						PatchManagerSettings.SetPersistentActive(patchID, false);
				}
			}
			return task;
		}

		private static Task UnpatchFast(MethodBase original, Harmony instance)
		{
			if (!original.HasMethodBody()) return Task.CompletedTask;
			return Task.Run(() =>
			{
				try
				{
					var patches = Harmony.GetPatchInfo(original);
					patches.Postfixes.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
					patches.Prefixes.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
					patches.Transpilers.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
					patches.Finalizers.Do(patchInfo => instance.Unpatch(original, patchInfo.PatchMethod));
					InternalMarkChanged();
					if (AllowDebugLogs) Debug.Log("Successfully unpatched " + original.FullDescription());
				}
				catch (Exception e)
				{
					Debug.LogWarning(e);
				}
			});
		}

		public static bool IsWaitingForLoad(string patchId) => WaitingForActivation.Contains(patchId);

		private static readonly HashSet<string> WaitingForActivation = new HashSet<string>();

		private static async Task<bool> ApplyPatch(Harmony instance, EditorPatchProviderInfo provider, HashSet<string> waitList)
		{
			if (waitList.Contains(provider.PatchID)) return false; 
			waitList.Add(provider.PatchID); 
			while (true)
			{
				if (!waitList.Contains(provider.PatchID)) return false;
				if (provider.Instance.AllPatchesAreReadyToLoad()) break;
				await Task.Delay(20);
			}
			
			// wait for harmony debug patch to finish before loading other patches
			var patchId = typeof(HarmonyUnityDebugLogPatch).FullName;  
			while (AllowDebugLogs && provider.PatchID != patchId && IsPersistentEnabled(patchId) && IsWaitingForLoad(patchId))
			{
				await Task.Delay(1);
			}

			if (!waitList.Contains(provider.PatchID)) return false;

			if (AllowDebugLogs)
				Debug.Log("APPLY PATCH: " + provider.PatchID);

			var patchData = provider.Data;
			var allMethodsPatchedSuccessfully = true;
			if(patchData.Count <= 0) Debug.LogWarning("<b>No patches</b> returned by " + provider.PatchID);
			for (var i = 0; i < patchData.Count; i++)
			{
				var data = patchData[i];
				if (data == null)
				{
					Debug.LogError("<b>Patch [" + i + "] is null</b> in " + provider.PatchID);
					continue;
				}

				if (data.PatchedMethods == null) data.PatchedMethods = new List<MethodBase>();

				var patch = data.EditorPatch;
				// var allowLogs = AllowDebugLogs;
				try
				{
					var data1 = data;
					var receivedUnityMainThreadException = false;

					async Task Patch()
					{
						var methods = await patch.GetTargetMethods();
						if (methods.Count <= 0)
							Debug.LogWarning("<b>No methods</b> returned by " + patch + "\n" + provider.PatchID);
						for (var index = 0; index < methods.Count; index++)
						{
							var method = methods[index];
							if (method == null)
							{
								Debug.LogError("<b>Method [" + index + "] is null</b> returned from patch " + patch + "\n" + provider.PatchID);
								continue;
							}

							if (!waitList.Contains(provider.PatchID)) break;
							try
							{
								if(AllowDebugLogs)
									Debug.Log("Patch: " + method);
								instance.Patch(
									method,
									data1.PrefixMethod != null ? new HarmonyMethod(data1.PrefixMethod) : null,
									data1.PostfixMethod != null ? new HarmonyMethod(data1.PostfixMethod) : null,
									data1.TranspilerMethod != null ? new HarmonyMethod(data1.TranspilerMethod) : null,
									data1.FinalizerMethod != null ? new HarmonyMethod(data1.FinalizerMethod) : null
								);
								// add method after successfully patching. When using Unpatch fast mode we dont do any unnecessary work in case patching didnt work
								data1.PatchedMethods.Add(method);
								if (AllowDebugLogs) Debug.Log("Successfully patched " + method.FullDescription());
							}
							catch (TargetInvocationException e)
							{
								provider.Instance.EnableException = e;
								allMethodsPatchedSuccessfully = false;
								if (e.InnerException != null)
								{
									if (e.InnerException.IsOrHasUnityException_CanOnlyBeCalledFromMainThread())
										receivedUnityMainThreadException = true;

									if (!SuppressAllExceptions)
									{
										var ex = e.InnerException;
										var allowLog = !(ex.IsOrHasUnityException_CanOnlyBeCalledFromMainThread() && provider.Instance.SuppressUnityExceptions);
										if (allowLog)
										{
											Debug.LogWarning(provider.Instance.Name + " " + ex.GetType() + ": " + ex.Message + "\n\n" + method.Name + " (" +
											                 method.DeclaringType?.FullName + ")"
											                 + "\n\nFull Stacktrace:\n" + ex.StackTrace);
										}
									}
								}
							}
							catch (NotSupportedException e)
							{
								provider.Instance.EnableException = e;
								allMethodsPatchedSuccessfully = false;
								if (!SuppressAllExceptions)
									Debug.LogWarning("Patching \"" + provider.Instance.Name + "\" is not supported: " + e.Message + "\n" + e);
							}
							catch (Exception e)
							{
								provider.Instance.EnableException = e;
								allMethodsPatchedSuccessfully = false;
								if (!SuppressAllExceptions)
								{
									Debug.LogError(provider.Instance.Name + ": " +  e);
									// Debug.LogWarning(provider.Instance.Name + " " + e.GetType() + ": " + e.Message + "\n\n" + method.Name + " (" +
								 //                  method.DeclaringType?.FullName + ")"
								 //                  + "\n\nFull Stacktrace:\n" + e.StackTrace);
								} 
							}
						}
					}

					if (provider.Instance.PatchThreaded)
					{
						await Task.Run(Patch);
						// some Unity methods can only be patched on the main thread
						if (!allMethodsPatchedSuccessfully && receivedUnityMainThreadException)
						{
							allMethodsPatchedSuccessfully = true;
							receivedUnityMainThreadException = false;
							await Patch();
						}
					}
					else await Patch();

					if (!waitList.Contains(provider.PatchID))
					{
						instance.UnpatchAll(provider.PatchID);
					}
				}
				catch (Exception e)
				{
					Debug.LogError("Patching failed " + provider.PatchID + "\n" + e);
					instance.UnpatchAll(provider.PatchID);
					break;
				}
			}

			if (waitList.Contains(provider.PatchID))
				waitList.Remove(provider.PatchID);
			
			return allMethodsPatchedSuccessfully;
		}

		internal static string HarmonyLogPath => Application.dataPath + "/../Logs/harmony.log.txt";

		private static void SetHarmonyDebugState(bool state)
		{
			if (Harmony.DEBUG == state) return;
			Harmony.DEBUG = state;
			if (state)
			{
				var fullPath = HarmonyLogPath;
				var dir = Path.GetDirectoryName(fullPath);
				if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
				Environment.SetEnvironmentVariable("HARMONY_LOG_FILE", fullPath);
				Debug.Log("Set Harmony debug path to " + fullPath);
			}
		}


		private static Task<bool> _completedTaskFailed;
		public static Task<bool> CompletedTaskFailed
		{
			get
			{
				if(_completedTaskFailed == null) _completedTaskFailed = Task.FromResult(false);
				return _completedTaskFailed;
			}
		}
		
		private static Task<bool> _completedTaskSuccess;
		public static Task<bool> CompletedTaskSuccess
		{
			get
			{
				if(_completedTaskSuccess == null) _completedTaskSuccess = Task.FromResult(true);

				return _completedTaskSuccess;
			}
		}
	}
}