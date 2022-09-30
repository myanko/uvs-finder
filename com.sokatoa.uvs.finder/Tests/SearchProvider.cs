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
            var results = UVSSearchProvider.PerformSearchInCurrentScript("", StateSearchContext.All);
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
            var results = UVSSearchProvider.PerformSearchInCurrentScript(keyword, StateSearchContext.All);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        public void PerformSearchCurrent_State_With_Keyword_Returns_Some_Results()
        {
            OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript("furet", StateSearchContext.All);
            Assert.AreEqual(1, results.Count);
        }

        [Test]
        // Those are state -> Flow
        [TestCase("furet42scriptstate", "", StateSearchContext.Children, 3)]
        [TestCase("furet42scriptstate", "", StateSearchContext.All, 19)]
        [TestCase("furet42scriptstate", "", StateSearchContext.Current, 3)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.Children, 0)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.All, 5)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.Current, 0)]
        // those are state -> state -> Flow
        [TestCase("furet2embed", "", StateSearchContext.Children, 10)]
        [TestCase("furet2embed", "", StateSearchContext.All, 19)]
        [TestCase("furet2embed", "", StateSearchContext.Current, 2)]
        [TestCase("furet2embed", "furet", StateSearchContext.Children, 3)]
        [TestCase("furet2embed", "furet", StateSearchContext.All, 5)]
        [TestCase("furet2embed", "furet", StateSearchContext.Current, 1)]
        public void PerformSearchCurrent_State_With_StateContext_Returns_Some_Results(string childGraphTitle, string keyword, StateSearchContext context, int result)
        {
            var graphReference = OpenVisualScriptAsset("Assets/TestAsset/furetstategraph1.asset");
            // open a substate
            OpenSubGraph(graphReference, childGraphTitle);
            
            var results = UVSSearchProvider.PerformSearchInCurrentScript(keyword, context);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        [TestCase("", 75)] 
        [TestCase("furet", 11)]
        [TestCase("Furet", 11)]
        [TestCase("FURET", 11)]
        [TestCase("unnamed", 3)] // special naming
        [TestCase("[", 24)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        public void PerformSearchAll_With_Keyword_Returns_Some_Results(string keyword, int result)
        {
            var results = UVSSearchProvider.PerformSearchAll(keyword);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        [TestCase("", 0)]
        [TestCase("furet", 0)]
        [TestCase("furetstategraph1", 0)]
        [TestCase("furetstategraph1 [Macro State]", 1)]
        [TestCase("unnamed", 0)] // special naming
        [TestCase("[", 0)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        public void PerformSearchAll_With_Keyword_Exact_Returns_Some_Results(string keyword, int result)
        {
            var results = UVSSearchProvider.PerformSearchAll(keyword, true);
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

        private GraphReference OpenVisualScriptAsset(string assetPath)
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
            return graphReference;
        }

        private GraphReference OpenSubGraph(GraphReference graphReference, string childGraphTitle)
        {
            GraphReference stateGraphReference = graphReference;
            foreach (var state in (graphReference.graph as StateGraph).states)
            {
                if (state is INesterState)
                {
                    if ((state as INesterState).childGraph?.title == childGraphTitle || (state as INesterState).nest?.macro?.graph?.title == childGraphTitle)
                    {
                        stateGraphReference = graphReference.ChildReference((INesterState)state, false);
                    }
                }
            }
            // open the window
            GraphWindow.OpenActive(stateGraphReference);
            
            return stateGraphReference;
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
