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
namespace Unity.VisualScripting.UVSFinder.Tests { 

    public class SearchProvider : MonoBehaviour
    {
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
        [TestCase("\"", 0)]
        public void PerformSearchAll_With_Keyword_Exact_Returns_Some_Results(string keyword, int result)
        {
            var results = UVSSearchProvider.PerformSearchAll(keyword, true);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        [TestCase("", 78)]
        [TestCase("furet", 12)]
        [TestCase("Furet", 12)]
        [TestCase("FURET", 12)]
        [TestCase("unnamed", 3)] // special naming
        [TestCase("[", 26)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        [TestCase("\"", 5)]
        public void PerformSearchAll_With_Keyword_Returns_Some_Results(string keyword, int result)
        {
            var results = UVSSearchProvider.PerformSearchAll(keyword);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        public void PerformSearchCurrent_Script_Returns_All_Results()
        {
            Utilities.OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript("", StateSearchContext.All);
            Assert.AreEqual(4, results.Count);
        }

        [Test]
        [TestCase("", 4)]
        [TestCase("furet", 2)]
        [TestCase("Furet", 2)]
        [TestCase("FURET", 2)]
        [TestCase("[", 3)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        [TestCase("\"", 1)]
        public void PerformSearchCurrent_Script_With_Keyword_Returns_Some_Results(string keyword, int result)
        {
            Utilities.OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var results = UVSSearchProvider.PerformSearchInCurrentScript(keyword, StateSearchContext.All);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        // this is the entry point of the state graph
        [TestCase("", "", StateSearchContext.Children, 21)]
        [TestCase("", "", StateSearchContext.All, 21)]
        [TestCase("", "", StateSearchContext.Current, 3)]
        [TestCase("", "furet", StateSearchContext.Children, 5)]
        [TestCase("", "furet", StateSearchContext.All, 5)]
        [TestCase("", "furet", StateSearchContext.Current, 2)]
        // Those are state -> Flow
        [TestCase("furet42scriptstate", "", StateSearchContext.Children, 3)]
        [TestCase("furet42scriptstate", "", StateSearchContext.All, 21)]
        [TestCase("furet42scriptstate", "", StateSearchContext.Current, 3)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.Children, 0)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.All, 5)]
        [TestCase("furet42scriptstate", "furet", StateSearchContext.Current, 0)]
        // those are state -> state -> Flow
        [TestCase("furet2embed", "", StateSearchContext.Children, 12)]
        [TestCase("furet2embed", "", StateSearchContext.All, 21)]
        [TestCase("furet2embed", "", StateSearchContext.Current, 2)]
        [TestCase("furet2embed", "furet", StateSearchContext.Children, 3)]
        [TestCase("furet2embed", "furet", StateSearchContext.All, 5)]
        [TestCase("furet2embed", "furet", StateSearchContext.Current, 1)]
        public void PerformSearchCurrent_State_With_StateContext_Returns_Some_Results(string childGraphTitle, string keyword, StateSearchContext context, int result)
        {
            var graphReference = Utilities.OpenVisualScriptAsset("Assets/TestAsset/furetstategraph1.asset");
            // open a substate
            if (childGraphTitle != "")
            {
                Utilities.OpenSubGraph(graphReference, childGraphTitle);
            }
            
            var results = UVSSearchProvider.PerformSearchInCurrentScript(keyword, context);
            Assert.AreEqual(result, results.Count);
        }

        [Test]
        [TestCase("", 42)]
        [TestCase("furet", 7)]
        [TestCase("Furet", 7)]
        [TestCase("FURET", 7)]
        [TestCase("[", 11)]
        [TestCase("notexisting", 0)]
        [TestCase("(", 0)]
        [TestCase("*", 0)]
        [TestCase("\"", 8)]
        public void PerformSearchHierarchy_With_All_Scenes_Opened_And_Keyword_Returns_Some_Results(string keyword, int result)
        {
            Utilities.OpenAllScenes();
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
    }
}
