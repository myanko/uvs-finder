using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace needle.EditorPatching
{
    public static class EditorUtils
    {
        private static FieldInfo currentSkinField;
        
        public static bool GUISkinHasLoaded()
        {
            if (currentSkinField == null)
            {
                currentSkinField = typeof(GUISkin).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
                if (currentSkinField == null) return false;
            }
            var skin = (GUISkin) currentSkinField.GetValue(null);
            if (skin == null) return false;
            if (skin.name == "GameSkin") return false;
            return true;
        }
        
        public static async Task AwaitEditorReady()
        {
            while (EditorApplication.isCompiling || EditorApplication.isUpdating) await Task.Delay(5);
        }

        
#if UNITY_2019_4
        private static PropertyInfo getFocusProperty;
#endif
        
        internal static bool ProjectSettingsOpenAndFocused()
        {
            var proj = projectSettingsWindow;
#if UNITY_2019_4
            if (getFocusProperty == null)
            {
                getFocusProperty = proj.GetType().GetProperty("hasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
                if (getFocusProperty == null) throw new Exception("Could not find EditorWindow.hasFocus property");
            }
            return proj && (bool)getFocusProperty.GetValue(proj);
#else
            return proj && proj.hasFocus;
#endif
        }
        
        private static EditorWindow _projectSettingsWindow;
        private static EditorWindow projectSettingsWindow
        {
            get
            {
                foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                {
                    if (window.GetType().FullName == "UnityEditor.ProjectSettingsWindow")
                    {
                        _projectSettingsWindow = window;
                        break;
                    }
                }
                return _projectSettingsWindow;
            }
        }
    }
}