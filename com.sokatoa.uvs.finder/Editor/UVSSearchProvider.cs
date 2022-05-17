using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;

#if !SUBGRAPH_RENAME
using SubgraphUnit = Unity.VisualScripting.SuperUnit;
using ScriptMachine = Unity.VisualScripting.FlowMachine;
#endif

namespace Unity.VisualScripting.UVSFinder
{
    public class UVSSearchProvider
    {
        // finds all the results nodes from the current script opened
        // sets the searchItems with the nodes found as a GraphItem
        public static List<ResultItem> PerformSearchInCurrentScript(string keyword)
        {
            var graphWindow = EditorWindow.GetWindow<GraphWindow>();

            // no graph opened to search in...
            if (graphWindow?.reference?.serializedObject == null)
            {
                return new List<ResultItem>();
            }

            try
            {
                ResultItemList itemsFound = new ResultItemList();
                itemsFound = GetElementsFromCanvas(GraphWindow.active?.reference?.graph?.Canvas(), keyword, itemsFound);
                return itemsFound.list;
            } catch (Exception e)
            {
                Debug.Log($"encountered an error while searching in current script: {e}");
            }

            return new List<ResultItem>();
        }

        public static List<ResultItem> PerformSearchInHierarchy(string keyword)
        {
            var searchItems = new ResultItemList();
            var searchTermLowerInvariant = CleanString(keyword);
            foreach (UnityEngine.Object o in GameObject.FindObjectsOfType<ScriptMachine>())
            {
                var scriptMachine = o.GetComponent<ScriptMachine>();
                if (scriptMachine?.nest?.source == GraphSource.Embed)
                {
                    searchItems = GetElementsFromScriptMachine(scriptMachine, o.GameObject().scene.path, searchTermLowerInvariant, searchItems);
                }

                var stateMachine = o.GetComponent<StateMachine>();
                if (stateMachine?.nest?.source == GraphSource.Embed)
                {
                    searchItems = GetElementsFromStateGraph(stateMachine.GetReference().AsReference(), stateMachine.graph, o.GameObject().scene.path, searchTermLowerInvariant, searchItems);
                }
            }
            
            return searchItems.list;
        }

        // finds all the results nodes from the asset files
        // TODO:
        // - process the files async to speed up the lookup
        public static List<ResultItem> PerformSearchAll(string keyword)
        {
            var searchItems = new ResultItemList();
            try {
                string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromScriptGraphAssetGuid(guid, keyword, searchItems);
                }
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromStateGraphAssetGuid(guid, keyword, searchItems);
                }

                // Search prefabs
                var searchTermLowerInvariant = CleanString(keyword);
                var paths = AssetDatabase.GetAllAssetPaths().Select(path => Path.Combine(Paths.project, path)).Where(File.Exists).Where(f => Path.GetExtension(f) == ".prefab");
                foreach (var p in paths)
                {
                    var assetPath = p.Remove(0, Paths.project.Length + 1);
                    UnityEngine.Object o = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if(o != null)
                    {
                        try
                        {
                            GameObject go = (GameObject)o;
                            var scriptMachine = go.GetComponent<ScriptMachine>();
                            if (scriptMachine?.nest?.source == GraphSource.Embed)
                                searchItems = GetElementsFromScriptMachine(scriptMachine, assetPath, searchTermLowerInvariant, searchItems);

                            var stateMachine = go.GetComponent<StateMachine>();
                            if (stateMachine?.nest?.source == GraphSource.Embed)
                                searchItems = GetElementsFromStateGraph(stateMachine.GetReference().AsReference(), stateMachine.graph, assetPath, searchTermLowerInvariant, searchItems);
                        } catch (Exception e)
                        {
                            Debug.Log($"Error while loading prefabs to search from them in path {assetPath} {e.Message} {e.StackTrace}");
                        }
                    }
                }
            } catch(Exception e){
                Debug.Log($"encountered an error while searching in all scripts {e.Message} {e.StackTrace}");
            }

            return searchItems.list;
        }

        private static ResultItemList FindNodesFromScriptGraphAssetGuid(string guid, string keyword, ResultItemList searchItems)
        {
            var searchTermLowerInvariant = CleanString(keyword);
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (sga?.graph?.elements.Count() > 0)
            {
                foreach (var e in sga.graph.elements)
                {
                    searchItems = GrabElements(e, null, null, sga.GetReference().AsReference(), null, assetPath, searchTermLowerInvariant, searchItems);
                }
            }

            return searchItems;
        }

        private static ResultItemList FindNodesFromStateGraphAssetGuid(string guid, string keyword, ResultItemList searchItems)
        {
            var searchTermLowerInvariant = CleanString(keyword);
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
            // pick up the first layer's elements
            if (sga?.graph?.elements.Count() > 0)
            {
                //Debug.Log($"stategraphasset {sga.name} has {sga.graph?.elements.Count()} elements");
                GetElementsFromStateGraph(sga.GetReference().AsReference(), sga.graph, assetPath, searchTermLowerInvariant, searchItems);
            }
            
            return searchItems;
        }

        // only grab the current level of the canvas. Do not recurse in subelements
        private static ResultItemList GetElementsFromCanvas(ICanvas canvas, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (canvas.graph == null || canvas.graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var e in canvas.graph.elements)
            {
                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                {
                    //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(e)}",
                        assetPath = "",
                        graphReference = GraphWindow.active?.reference,
                        graphGuid = e.guid.ToString(),
                        graphElement = e
                    });
                }
            }

            return searchItems;
        }

        private static ResultItemList GetElementsFromScriptMachine(ScriptMachine scriptMachine, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if(scriptMachine == null || scriptMachine.graph.elements.Count() == 0)
            {
                return searchItems;
            }

            var reference = scriptMachine.GetReference().AsReference();
            foreach (var e in scriptMachine.graph.elements)
            {
                searchItems = GrabElements(e, "", scriptMachine.gameObject, reference, scriptMachine.graph, assetPath, searchTermLowerInvariant, searchItems);
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromIGraph(GraphReference reference, IGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (graph == null || graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var e in graph.elements)
            {
                //searchItems = GrabElements(e, typeof(StateGraphAsset), "", null, reference, graph, assetPath, searchTermLowerInvariant, searchItems);
                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                {
                    //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(e)}",
                        assetPath = assetPath,
                        graphReference = reference,
                        graphGuid = e.guid.ToString(),
                        graphElement = e
                    });
                }
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromSubGraph(GraphReference reference, FlowGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            // get this layer's elements
            searchItems = GetElementsFromIGraph(reference, graph, assetPath, searchTermLowerInvariant, searchItems);
            // get the subgraph's elements
            if (graph.elements.Count() > 0)
            {
                foreach (var e in graph.elements)
                {
                    searchItems = GrabElements(e, graph.title, null, reference, graph, assetPath, searchTermLowerInvariant, searchItems);
                }
            }

            return searchItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="reference">the reference to the graph</param>
        /// <param name="graph">The graph we are currently looking at (which can be a child/nested graph)</param>
        /// <param name="assetPath"></param>
        /// <param name="searchTermLowerInvariant"></param>
        /// <param name="searchItems"></param>
        /// <returns></returns>
        private static ResultItemList GetElementsFromStateGraph(GraphReference reference, StateGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (graph == null) {
                return searchItems;
            }
            // get this layer's elements
            searchItems = GetElementsFromIGraph(reference, graph, assetPath, searchTermLowerInvariant, searchItems);
            
            // get each states' elements
            if (graph.states.Count() > 0)
            {
                foreach (var state in graph.states)
                {
                    IGraph childGraph = ((INesterState)state).childGraph;
                    if (state is INesterState && childGraph?.elements.Count() > 0)
                    {
                        //Debug.Log($"state {state.guid} {((INesterState)state).childGraph.title} has {((INesterState)state).childGraph.elements.Count()} elements");
                        foreach (var e in childGraph.elements)
                        {
                            var stateName = !String.IsNullOrEmpty(childGraph.title) ? childGraph.title : "Script State";
                            searchItems = GrabElements(e, stateName, null, reference.ChildReference((INesterState)state, false), childGraph, assetPath, searchTermLowerInvariant, searchItems); 
                        }
                    }
                }
            }
            return searchItems;
        }

        private static ResultItemList GrabElements(IGraphElement e, string stateName, GameObject gameObject, GraphReference reference, IGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (e is StateUnit)
            {
                if (((StateUnit)e).nest?.source == GraphSource.Embed && ((StateUnit)e).nest?.graph?.elements.Count() > 0)
                {
                    //searchItems = GetElementsFromIGraph(reference, ((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                    // grab the current element
                    var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                    {
                        //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e
                        });
                    }
                    // children
                    searchItems = GetElementsFromStateGraph(reference.ChildReference((StateUnit)e, false), ((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                }
            }
            else if (e is SubgraphUnit)
            {
                if (((SubgraphUnit)e).nest?.source == GraphSource.Embed && ((SubgraphUnit)e).nest?.graph?.elements.Count() > 0)
                {
                    //searchItems = GetElementsFromIGraph(reference, ((SubgraphUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                    // grab the current element
                    var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                    {
                        //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e
                        });
                    }
                    searchItems = GetElementsFromSubGraph(reference.ChildReference(((SubgraphUnit)e), false), ((SubgraphUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                }
            }
            else
            {
                try
                {
                    var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                    {
                        //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e,
                            stateName = stateName,
                            gameObject = gameObject
                        });
                    }
                } catch (Exception ex)
                {
                    Debug.Log($"Could not add element {e?.guid} {assetPath} {reference?.graph?.title}");
                }
            }

            return searchItems;
        }

        private static bool IsIgnoreElement(IGraphElement graphElement)
        {
            switch (graphElement.GetType().ToString())
            {
                case "Bolt.ControlConnection":
                case "Bolt.ValueConnection":
                case "Unity.VisualScripting.ControlConnection":
                case "Unity.VisualScripting.ValueConnection":
                    return true;
            }

            return false;
        }

        private static string CleanString(string keyword)
        {
            return keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
        }
    }

    // this is used to have a list of distinct items only
    // it might be covering the fact that I seem to search more than once on the same items
    // but let's go with this for now.
    public class ResultItemList
    {
        public List<ResultItem> list = new List<ResultItem>();
        // Graphs can contain the same guids (especially if you duplicate things
        // so we need the asset path too to discriminate
        public void AddDistinct(ResultItem item)
        {
            bool isInList = false;
            foreach (var i in list)
            {
                if (i.graphGuid == item.graphGuid && i.assetPath == item.assetPath)
                {
                    isInList = true;
                }
            }
            if (!isInList)
            {
                list.Add(item);
            }
        }
    }
    public class ResultItem{
        public string graphGuid; // Visual scripting GUID (not file guid)
        public string itemName;
        public IGraphElement graphElement;
        public string assetPath;
        public GraphReference graphReference;
        public GameObject gameObject;
        public string stateName;
    }
}
