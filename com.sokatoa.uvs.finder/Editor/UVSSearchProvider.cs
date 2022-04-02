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
                var assetPath = AssetDatabase.GetAssetPath(graphWindow.reference.serializedObject);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var guid = AssetDatabase.GUIDFromAssetPath(assetPath);
                ResultItemList itemsFound = new ResultItemList();
                if (assetType == typeof(ScriptGraphAsset))
                {
                    assetType = typeof(ScriptGraphAsset);
                    itemsFound = FindNodesFromScriptGraphAssetGuid(guid.ToString(), keyword, itemsFound);
                    if (itemsFound != null)
                    {
                        return itemsFound.list;
                    }
                } else if(assetType == typeof(StateGraphAsset))
                {
                    itemsFound = FindNodesFromStateGraphAssetGuid(guid.ToString(), keyword, itemsFound);
                    if (itemsFound != null)
                    {
                        return itemsFound.list;
                    }
                } else if(assetType == typeof(GameObject))
                {
                    itemsFound = GetElementsFromScriptMachine(graphWindow.reference.machine as ScriptMachine, assetPath, keyword, itemsFound);
                    if (itemsFound != null)
                    {
                        return itemsFound.list;
                    }
                }

                
                
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
            var scene = SceneManager.GetActiveScene();
            foreach (UnityEngine.Object o in GameObject.FindObjectsOfType(typeof(ScriptMachine)))
            {
                var scriptMachine = o.GetComponent<ScriptMachine>();
                if (scriptMachine?.nest?.source == GraphSource.Embed)
                    searchItems = GetElementsFromScriptMachine(scriptMachine, scene.path, searchTermLowerInvariant, searchItems);

                var stateMachine = o.GetComponent<StateMachine>();
                if (stateMachine?.nest?.source == GraphSource.Embed)
                    searchItems = GetElementsFromStateGraph(stateMachine, stateMachine.graph, scene.path, searchTermLowerInvariant, searchItems);
            }

            return searchItems.list;
        }

        // finds all the results nodes from the asset files
        // TODO:
        // - search for embedded scripts in scenes
        // - process the files async to speed up the lookup
        public static List<ResultItem> PerformSearchAll(string keyword)
        {
            var searchItems = new ResultItemList();
            try {
                string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
                //Debug.Log($"found {guids.Length} script graph assets");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromScriptGraphAssetGuid(guid, keyword, searchItems);
                }
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                //Debug.Log($"found {guids.Length} state graph assets");
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
                                searchItems = GetElementsFromStateGraph(stateMachine, stateMachine.graph, assetPath, searchTermLowerInvariant, searchItems);
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
                foreach (var a in sga.graph.elements)
                {
                    var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(a));
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(a))
                    {
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = GraphElement.GetElementName(a),
                            assetPath = assetPath,
                            graphGuid = a.guid.ToString(),
                            graphElement = a,
                            type = typeof(ScriptGraphAsset)
                        });
                    }
                    
                    // TODO: recurse in embedded elements somewhere here
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
                GetElementsFromStateGraph(null, sga.graph, assetPath, searchTermLowerInvariant, searchItems);
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromScriptMachine(ScriptMachine scriptMachine, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if(scriptMachine == null || scriptMachine.graph.elements.Count() == 0)
            {
                return searchItems;
            }

            foreach (var a in scriptMachine.graph.elements)
            {
                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(a));
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(a))
                {
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(a)}",
                        assetPath = assetPath,
                        graphReference = scriptMachine.GetReference().AsReference(),
                        graphGuid = a.guid.ToString(),
                        graphElement = a,
                        type = typeof(ScriptMachine),
                        gameObject = scriptMachine.gameObject
                    });
                }
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromIGraph(StateMachine machine, IGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (graph == null || graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var a in graph.elements)
            {
                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(a));
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(a))
                {
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(a)}",
                        assetPath = assetPath,
                        graphReference = machine?.GetReference().AsReference(),
                        graphGuid = a.guid.ToString(),
                        graphElement = a,
                        type = typeof(StateGraphAsset)
                    });
                }
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromIGraph(ScriptMachine machine, IGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (graph == null || graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var a in graph.elements)
            {
                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(a));
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(a))
                {
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(a)}",
                        assetPath = assetPath,
                        graphReference = machine?.GetReference().AsReference(),
                        graphGuid = a.guid.ToString(),
                        graphElement = a,
                        type = typeof(StateGraphAsset)
                    });
                }
            }

            return searchItems;
        }

        private static ResultItemList GetElementsFromSubGraph(StateMachine stateMachine, FlowGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            // get this layer's elements
            searchItems = GetElementsFromIGraph(stateMachine, graph, assetPath, searchTermLowerInvariant, searchItems);
            // get the subgraph's elements
            if (graph.elements.Count() > 0)
            {
                foreach (var e in graph.elements)
                {
                    if (e is StateUnit)
                    {
                        if (((StateUnit)e).nest?.source == GraphSource.Embed && ((StateUnit)e).nest?.graph?.elements.Count() > 0)
                        {
                            searchItems = GetElementsFromIGraph(stateMachine, ((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                            searchItems = GetElementsFromStateGraph(null, ((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                        }
                    }
                    else if (e is SubgraphUnit)
                    {
                        if (((SubgraphUnit)e).nest?.source == GraphSource.Embed && ((SubgraphUnit)e).nest?.graph?.elements.Count() > 0)
                        {
                            searchItems = GetElementsFromIGraph(stateMachine, ((SubgraphUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                            searchItems = GetElementsFromSubGraph(stateMachine, ((SubgraphUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                        }
                    }
                    else
                    {
                        var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                        if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                        {
                            //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                            searchItems.AddDistinct(new ResultItem()
                            {
                                itemName = $"{GraphElement.GetElementName(e)}",
                                assetPath = assetPath,
                                graphReference = stateMachine.GetReference().AsReference(),
                                graphGuid = e.guid.ToString(),
                                graphElement = e,
                                type = typeof(StateGraphAsset)
                            });
                        }
                    }
                }
            }

            return searchItems;
        }

        private static ResultItemList GetElementsFromSubGraph(ScriptMachine scriptMachine, FlowGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            // get this layer's elements
            searchItems = GetElementsFromIGraph(scriptMachine, graph, assetPath, searchTermLowerInvariant, searchItems);
            // get the subgraph's elements
            if (graph.elements.Count() > 0)
            {
                foreach (var e in graph.elements)
                {
                    if (e is StateUnit)
                    {
                        if (((StateUnit)e).nest?.source == GraphSource.Embed && ((StateUnit)e).nest?.graph?.elements.Count() > 0)
                        {
                            searchItems = GetElementsFromIGraph(scriptMachine, ((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                            searchItems = GetElementsFromStateGraph(null, ((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                        }
                    }
                    else if (e is SubgraphUnit)
                    {
                        if (((SubgraphUnit)e).nest?.source == GraphSource.Embed && ((SubgraphUnit)e).nest?.graph?.elements.Count() > 0)
                        {
                            searchItems = GetElementsFromIGraph(scriptMachine, ((SubgraphUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                            searchItems = GetElementsFromSubGraph(scriptMachine, ((SubgraphUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                        }
                    }
                    else
                    {
                        var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                        if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                        {
                            //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                            searchItems.AddDistinct(new ResultItem()
                            {
                                itemName = $"{GraphElement.GetElementName(e)}",
                                assetPath = assetPath,
                                graphReference = scriptMachine.GetReference().AsReference(),
                                graphGuid = e.guid.ToString(),
                                graphElement = e,
                                type = typeof(StateGraphAsset)
                            });
                        }
                    }
                }
            }

            return searchItems;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="stateMachine">the original state machine, can be null?</param>
        /// <param name="graph">The graph we are currently looking at (which can be a child/nested graph)</param>
        /// <param name="assetPath"></param>
        /// <param name="searchTermLowerInvariant"></param>
        /// <param name="searchItems"></param>
        /// <returns></returns>
        private static ResultItemList GetElementsFromStateGraph(StateMachine stateMachine, StateGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            if (graph == null) {
                return searchItems;
            }
            // get this layer's elements
            searchItems = GetElementsFromIGraph(stateMachine, graph, assetPath, searchTermLowerInvariant, searchItems);
            
            // get each states' elements
            if (graph.states.Count() > 0)
            {
                foreach (var state in graph.states)
                {
                    if (state is INesterState && ((INesterState)state).childGraph?.elements.Count() > 0)
                    {
                        //Debug.Log($"state {state.guid} {((INesterState)state).childGraph.title} has {((INesterState)state).childGraph.elements.Count()} elements");
                        foreach (var e in ((INesterState)state).childGraph.elements)
                        {
                            // recurse
                            if (e is StateUnit)
                            {
                                if (((StateUnit)e).nest?.source == GraphSource.Embed && ((StateUnit)e).nest?.graph?.elements.Count() > 0)
                                {
                                    searchItems = GetElementsFromIGraph(stateMachine, ((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                                    searchItems = GetElementsFromStateGraph(stateMachine, ((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                                }
                            }
                            else if(e is SubgraphUnit)
                            {
                                if (((SubgraphUnit)e).nest?.source == GraphSource.Embed && ((SubgraphUnit)e).nest?.graph?.elements.Count() > 0)
                                {
                                    searchItems = GetElementsFromIGraph(stateMachine, ((SubgraphUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                                    searchItems = GetElementsFromSubGraph(stateMachine, ((SubgraphUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                                }
                            }
                            else
                            {
                                var embedElementNameLowerInvariant = CleanString(GraphElement.GetElementName(e));
                                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant) && !IsIgnoreElement(e))
                                {
                                    //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                                    searchItems.AddDistinct(new ResultItem()
                                    {
                                        itemName = $"{GraphElement.GetElementName(e)}",
                                        assetPath = assetPath,
                                        graphReference = stateMachine?.GetReference().AsReference(),
                                        graphGuid = e.guid.ToString(),
                                        graphElement = e,
                                        type = typeof(StateGraphAsset)
                                    });
                                }
                            }
                        }
                    }
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
        public Type type; //The type of the node
        public GraphReference graphReference;
        public GameObject gameObject;
    }
}
