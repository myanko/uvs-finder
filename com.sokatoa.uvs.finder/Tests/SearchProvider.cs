using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using System;

/**
 * The reasoning behind the testing strategy is that the whole project contains 
 * testable data
 * so we call the search methods and validate what comes up
 */
namespace Unity.VisualScripting.UVSFinder { 

    public class SearchProvider : MonoBehaviour
    {
        [Test]
        public void PerformSearchCurrent_Script_Returns_All_Results()
        {
            OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript("");
            Assert.AreEqual(3, results.Count);
        }

        [Test]
        [TestCase("", 3)]
        [TestCase("furet", 1)]
        [TestCase("Furet", 1)]
        [TestCase("FURET", 1)]
        [TestCase("[", 2)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        public void PerformSearchCurrent_Script_With_Keyword_Returns_Some_Results(string keyword, int result)
        {
            OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript(keyword);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        public void PerformSearchCurrent_State_With_Keyword_Returns_Some_Results()
        {
            OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript("furet");
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        [TestCase("", 55)]
        [TestCase("furet", 9)]
        [TestCase("Furet", 9)]
        [TestCase("FURET", 9)]
        [TestCase("[", 19)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        public void PerformSearchAll_With_Keyword_Returns_Some_Results(string keyword, int result)
        {
            var results = UVSSearchProvider.PerformSearchAll(keyword);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        [TestCase("", 27)]
        [TestCase("furet", 4)]
        [TestCase("Furet", 4)]
        [TestCase("FURET", 4)]
        [TestCase("[", 6)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        public void PerformSearchHierarchy_With_All_Scenes_Opened_And_Keyword_Returns_Some_Results(string keyword, int result)
        {
            OpenAllScenes();
            var results = UVSSearchProvider.PerformSearchInHierarchy(keyword);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        public void PerformSearchHierarchy_With_One_Scene_Opened_Returns_All_Results()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var results = UVSSearchProvider.PerformSearchInHierarchy("");
            Assert.AreEqual(14, results.Count);
        }

        [Test]
        public void PerformSearchHierarchy_With_One_Scene_Opened_And_Keyword_Returns_Some_Results()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            var results = UVSSearchProvider.PerformSearchInHierarchy("furet");
            Assert.AreEqual(2, results.Count);
        }

        private void OpenVisualScriptAsset(string assetPath)
        {
            GraphReference graphReference;
            Type t = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            if (t == typeof(ScriptGraphAsset))
            {
                var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
                graphReference = GraphReference.New(sga, true);
            }
            else
            {
                var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
                graphReference = GraphReference.New(sga, true);
            }
            // open the window
            GraphWindow.OpenActive(graphReference);
        }
        private void OpenAllScenes()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            string[] scenes = AssetDatabase.FindAssets("t:scene", new[] { "Assets" });
            foreach (string guid in scenes)
            {
                var myCurrentScenePath = AssetDatabase.GUIDToAssetPath(guid);
                var myCurrentSceneName = Path.GetFileName(myCurrentScenePath);
                if (myCurrentSceneName != "SampleScene.unity")
                {
                    EditorSceneManager.OpenScene(myCurrentScenePath, OpenSceneMode.Additive);
                }
            }
        }

    }
}
