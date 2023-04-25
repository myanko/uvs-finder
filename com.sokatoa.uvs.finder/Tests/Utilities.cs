using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.IO;
using System;

/**
 * The reasoning behind the testing strategy is that the whole project contains 
 * testable data
 * so we call the search methods and validate what comes up
 */
namespace Unity.VisualScripting.UVSFinder.Tests { 

    public class Utilities : MonoBehaviour
    {
        public static GraphReference OpenVisualScriptAsset(string assetPath)
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

        public static GraphReference OpenSubGraph(GraphReference graphReference, string childGraphTitle)
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

        public static void OpenAllScenes()
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
