using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using Assembly = System.Reflection.Assembly;

namespace needle.EditorPatching
{
	public static class AssemblyExtensions
	{
		public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(t => t != null);
			}
		}

		internal static string GetGroupName(this Assembly assembly)
		{
			var path_assemblyDef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.GetName().Name);

			bool TryGetPackageName(string path, out string packageName)
			{
				// TODO: handle "Assets/" location
				const string packageKey = "Packages/";
				if (!string.IsNullOrEmpty(path) && path.StartsWith(packageKey))
				{
					var sub0 = path.Substring(packageKey.Length);
					packageName = sub0.Substring(0, sub0.IndexOfAny(new[] {'/', '\\'}));
					// get last part
					packageName = packageName.Substring(packageName.LastIndexOf(".", StringComparison.Ordinal)+1);
					packageName = packageName.Replace("-", " ");

					// packageName = ObjectNames.NicifyVariableName(packageName);
					var textInfo = new CultureInfo("en-US",false).TextInfo;
					packageName = textInfo.ToTitleCase(packageName);
					return true;
				}

				packageName = null;
				return false;
			}

			if (TryGetPackageName(path_assemblyDef, out var pn))
				return pn;
			
			// if assembly name could not be found
			return assembly.GetName().Name;
		}
		
		
		private static string FirstCharToUpper(string input)
		{
			switch (input)
			{
				case null: throw new ArgumentNullException(nameof(input));
				case "": throw new ArgumentException($"{nameof(input)} cannot be empty", nameof(input));
				default: return input.First().ToString().ToUpper() + input.Substring(1);
			}
		}
	}
}