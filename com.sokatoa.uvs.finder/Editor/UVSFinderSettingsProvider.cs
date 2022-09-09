using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Collections.Generic;
using UnityEngine.UI;

namespace Unity.VisualScripting.UVSFinder
{
    internal class UVSFinderSettingsProvider : SettingsProvider
    {
        private const string PreferencePath = "Preferences/Visual Scripting/UVS Finder";
        private static string UIPath = new UVSFinderPaths().findRootPackagePath()+ "/UI/";

        private static UVSFinderSettingsProvider provider;
        private static UVSFinderPreferences preferences;
        
        public static UVSFinderPreferences Preferences 
        {
            get
            {
                if (!preferences)
                    LoadFromJson();
                return preferences;
            }
        }

        public static event Action onChange;
        
        private SerializedObject serializedObject;
        
        private UVSFinderSettingsProvider(string path, SettingsScope scope)
            : base(path, scope){}

        public override void OnActivate(string searchContext, VisualElement root)
        {
            if (!preferences)
                LoadFromJson();
                
            serializedObject = new SerializedObject(preferences);
            keywords = GetSearchKeywordsFromSerializedObject(serializedObject);

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UIPath + "UVSFinder_settings.uxml");
            visualTree.CloneTree(root);
            
            var scrollView = root.Query<ScrollView>().First();
            var container = scrollView.contentContainer;
            
            ApplyStyling(root);
            root.Bind(serializedObject);
            var dropdown = root.Q<DropdownField>("StateContext");
            dropdown.SetValueWithoutNotify(preferences.stateSearchContext.DisplayName());
            dropdown.RegisterValueChangedCallback((ChangeEvent<string> e) => SaveToJson());

            var color = root.Q<ColorField>();
            color.value = preferences.textHighLightColor;
        }

        public override void OnDeactivate()
        {
            SaveToJson();
        }

        private static void ApplyStyling(VisualElement root)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "preferences-style.uss");
            var foldoutStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "foldout-header.uss");
            root.styleSheets.Add(styleSheet);
            root.styleSheets.Add(foldoutStyle);

            if (EditorGUIUtility.isProSkin)
            {
                var foldoutDarkStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIPath + "foldout-header_dark.uss");
                root.styleSheets.Add(foldoutDarkStyle);
            }
        }
        
        private static void LoadFromJson()
        {
            if (!preferences)
                preferences = ScriptableObject.CreateInstance<UVSFinderPreferences>();

            var json = EditorPrefs.GetString(PreferencePath);
            JsonUtility.FromJsonOverwrite(json, preferences);
        }

        private static void SaveToJson()
        {
            if (!preferences)
                return;
        
            var json = JsonUtility.ToJson(preferences, true);
            EditorPrefs.SetString(PreferencePath, json);
            
            onChange?.Invoke();
        }

        [SettingsProvider]
        private static SettingsProvider GetSettingsProvider()
        {
            return provider ?? (provider = new UVSFinderSettingsProvider(PreferencePath, SettingsScope.User));
        }
    }
}