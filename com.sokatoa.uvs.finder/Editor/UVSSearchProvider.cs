using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;

#if !SUBGRAPH_RENAME
using SubgraphUnit = Unity.VisualScripting.SuperUnit;
using ScriptMachine = Unity.VisualScripting.FlowMachine;
#endif

// Note:
// StateMachine -> StateGraph -> FlowGraph/ScriptGraph
// ScriptMachine -> ScriptGraph
namespace Unity.VisualScripting.UVSFinder
{
    public class UVSSearchProvider
    {
        
        // finds all the results nodes from the current script opened
        // sets the searchItems with the nodes found as a GraphItem
        public static List<ResultItem> PerformSearchInCurrentScript(string keyword, StateSearchContext stateSearchContext, bool isExact = false)
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
                var searchContext = new SearchContext()
                {
                    keyword = keyword,
                    isExactSearchTerm = isExact
                };
                var canvas = GraphWindow.active?.reference?.graph?.Canvas();
                if (GraphWindow.active?.reference.root.childGraph is StateGraph)
                {
                    switch (stateSearchContext)
                    {
                        case StateSearchContext.All:
                            {
                                // Make sure to move the reference back to the root to grab all the children as well
                                if (GraphWindow.active?.reference.root.childGraph is FlowGraph)
                                {
                                    itemsFound = GetElementsFromFlowGraph(GraphWindow.active?.reference.root.GetReference().AsReference(), (GraphWindow.active?.reference.root.childGraph as FlowGraph), "", searchContext, itemsFound);
                                }
                                else
                                {
                                    itemsFound = GetElementsFromStateGraph(GraphWindow.active?.reference.root.GetReference().AsReference(), (GraphWindow.active?.reference.root.childGraph as StateGraph), "", searchContext, itemsFound);
                                }
                                break;
                            }
                        case StateSearchContext.Children:
                            {
                                // Start at the current reference and go down
                                if (GraphWindow.active?.reference.graph is FlowGraph)
                                {
                                    itemsFound = GetElementsFromFlowGraph(GraphWindow.active?.reference, (GraphWindow.active?.reference.graph as FlowGraph), "", searchContext, itemsFound);
                                }
                                else
                                {
                                    // this will grab the current and child graph of the stategraph
                                    itemsFound = GetElementsFromStateGraph(GraphWindow.active?.reference, (GraphWindow.active?.reference.graph as StateGraph), "", searchContext, itemsFound);
                                }
                                break;
                            }
                        case StateSearchContext.Parent:
                            {
                                // TODO:
                                // Start where you are and recursively get the parent until you reach the root
                                itemsFound = GetElementsFromStateGraph(GraphWindow.active?.reference.root.GetReference().AsReference(), (GraphWindow.active?.reference.root.childGraph as StateGraph), "", searchContext, itemsFound);
                                break;
                            }
                        case StateSearchContext.Current:
                            {
                                // only grab the elements of the current canvas
                                itemsFound = GetElementsFromCanvas(canvas, searchContext, itemsFound);
                                break;
                            }
                    }
                }
                else
                {
                    // Also search in the canvas if the script is not a stategraph
                    itemsFound = GetElementsFromCanvas(canvas, searchContext, itemsFound);
                }

                return itemsFound.list;
            } catch (Exception e)
            {
                Debug.Log($"encountered an error while searching in current script: {e}");
            }

            
            
            return new List<ResultItem>();
        }

        public static List<ResultItem> PerformSearchInHierarchy(string keyword, bool isExact = false)
        {
            var searchItems = new ResultItemList();
            var searchContext = new SearchContext()
            {
                keyword = keyword,
                isExactSearchTerm = isExact
            };
            foreach (UnityEngine.Object o in GameObject.FindObjectsOfType<ScriptMachine>())
            {
                var scriptMachine = o.GetComponent<ScriptMachine>();
                if (scriptMachine?.nest?.source == GraphSource.Embed)
                {
                    searchItems = GetElementsFromScriptMachine(scriptMachine, o.GameObject().scene.path, searchContext, searchItems);
                }
            }
            foreach (UnityEngine.Object o in GameObject.FindObjectsOfType<StateMachine>())
            {
                var stateMachine = o.GetComponent<StateMachine>();
                if (stateMachine?.nest?.source == GraphSource.Embed)
                {
                    searchItems = GetElementsFromStateGraph(stateMachine.GetReference().AsReference(), stateMachine.graph, o.GameObject().scene.path, searchContext, searchItems);
                }
            }
            
            return searchItems.list;
        }

        // finds all the results nodes from the asset files
        // TODO:
        // - process the files async to speed up the lookup
        public static List<ResultItem> PerformSearchAll(string keyword, bool isExact = false)
        {
            var searchItems = new ResultItemList();
            var searchContext = new SearchContext()
            {
                keyword = keyword,
                isExactSearchTerm = isExact
            };
            try {
                string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromScriptGraphAssetGuid(guid, searchContext, searchItems);
                }
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromStateGraphAssetGuid(guid, searchContext, searchItems);
                }

                // Search prefabs
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
                                searchItems = GetElementsFromScriptMachine(scriptMachine, assetPath, searchContext, searchItems);

                            var stateMachine = go.GetComponent<StateMachine>();
                            if (stateMachine?.nest?.source == GraphSource.Embed)
                                searchItems = GetElementsFromStateGraph(stateMachine.GetReference().AsReference(), stateMachine.graph, assetPath, searchContext, searchItems);
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

        private static ResultItemList FindNodesFromScriptGraphAssetGuid(string guid, SearchContext searchContext, ResultItemList searchItems)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (sga?.graph?.elements.Count() > 0)
            {
                foreach (var e in sga.graph.elements)
                {
                    searchItems = GrabElements(e, null, null, sga.GetReference().AsReference(), null, assetPath, searchContext, searchItems);
                }
            }

            return searchItems;
        }

        private static ResultItemList FindNodesFromStateGraphAssetGuid(string guid, SearchContext searchContext, ResultItemList searchItems)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
            // pick up the first layer's elements
            if (sga?.graph?.elements.Count() > 0)
            {
                //Debug.Log($"stategraphasset {sga.name} has {sga.graph?.elements.Count()} elements");
                GetElementsFromStateGraph(sga.GetReference().AsReference(), sga.graph, assetPath, searchContext, searchItems);
            }
            
            return searchItems;
        }

        // only grab the current level of the canvas. Do not recurse in subelements
        private static ResultItemList GetElementsFromCanvas(ICanvas canvas, SearchContext searchContext, ResultItemList searchItems)
        {
            if (canvas.graph == null || canvas.graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var e in canvas.graph.elements)
            {
                if (isFoundElement(searchContext, e))
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

        private static ResultItemList GetElementsFromScriptMachine(ScriptMachine scriptMachine, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            
            if (scriptMachine == null || (scriptMachine.graph.elements.Count() == 0 && scriptMachine.nest?.embed?.elements.Count == 0))
            {
                return searchItems;
            }

            var reference = scriptMachine.GetReference().AsReference();
            foreach (var e in scriptMachine.graph.elements)
            {
                searchItems = GrabElements(e, "", scriptMachine.gameObject, reference, scriptMachine.graph, assetPath, searchContext, searchItems);
            }
            foreach (var e in scriptMachine.nest?.embed?.elements)
            {
                searchItems = GrabElements(e, "", scriptMachine.gameObject, reference, scriptMachine.graph, assetPath, searchContext, searchItems);
            }

            return searchItems;
        }

        private static ResultItemList GetElementsFromIGraph(GraphReference reference, IGraph graph, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            if (graph == null || graph.elements.Count == 0)
            {
                return searchItems;
            }

            foreach (var e in graph.elements)
            {
                if (isFoundElement(searchContext, e))
                {
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(e)}",
                        assetPath = assetPath,
                        graphReference = reference,
                        graphGuid = e.guid.ToString(),
                        graphElement = e,
                        stateName = graph.title
                    });
                }
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromFlowGraph(GraphReference reference, FlowGraph graph, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            // get this layer's elements
            searchItems = GetElementsFromIGraph(reference, graph, assetPath, searchContext, searchItems);
            // get the subgraph's elements
            if (graph.elements.Count() > 0)
            {
                foreach (var e in graph.elements)
                {
                    searchItems = GrabElements(e, graph.title, null, reference, graph, assetPath, searchContext, searchItems);
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
        /// <param name="searchContext"></param>
        /// <param name="searchItems"></param>
        /// <returns></returns>
        private static ResultItemList GetElementsFromStateGraph(GraphReference reference, StateGraph graph, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            if (graph == null) {
                return searchItems;
            }
            // get this layer's elements
            searchItems = GetElementsFromIGraph(reference, graph, assetPath, searchContext, searchItems);
            // get each states' elements
            if (graph.states.Count() > 0)
            {
                foreach (var transition in graph.transitions)
                {
                    if (transition is INesterStateTransition)
                    {
                        IGraph childGraph = ((INesterStateTransition)transition).childGraph;
                        if (childGraph?.elements.Count() > 0)
                        {
                            foreach (var e in childGraph.elements)
                            {
                                var stateName = !String.IsNullOrEmpty(childGraph.title) ? childGraph.title : "Script State";
                                if (childGraph is StateGraph)
                                {
                                    searchItems = GetElementsFromStateGraph(reference.ChildReference((INesterStateTransition)transition, false), (StateGraph)childGraph, assetPath, searchContext, searchItems);
                                }
                                else
                                {
                                    searchItems = GrabElements(e, stateName, null, reference.ChildReference((INesterStateTransition)transition, false), childGraph, assetPath, searchContext, searchItems);
                                }
                            }
                        }
                    }
                }

                foreach (var state in graph.states)
                {
                    if (state is INesterState)
                    {
                        IGraph childGraph = ((INesterState)state).childGraph;
                        if (childGraph?.elements.Count() > 0)
                        {
                            foreach (var e in childGraph.elements)
                            {
                                var stateName = !String.IsNullOrEmpty(childGraph.title) ? childGraph.title : "Script State";
                                if (childGraph is StateGraph)
                                {
                                    searchItems = GetElementsFromStateGraph(reference.ChildReference((INesterState)state, false), (StateGraph)childGraph, assetPath, searchContext, searchItems);
                                } else
                                {
                                    searchItems = GrabElements(e, stateName, null, reference.ChildReference((INesterState)state, false), childGraph, assetPath, searchContext, searchItems);
                                }
                            }
                        }
                    }
                }
                
            }
            return searchItems;
        }

        private static ResultItemList GrabElements(IGraphElement e, string stateName, GameObject gameObject, GraphReference reference, IGraph graph, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            if (e is StateUnit)
            {
                if (((StateUnit)e).nest?.source == GraphSource.Embed && ((StateUnit)e).nest?.graph?.elements.Count() > 0)
                {
                    //searchItems = GetElementsFromIGraph(reference, ((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                    // grab the current element
                    if (isFoundElement(searchContext, e))
                    {
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e,
                            stateName = stateName
                        });
                    }
                    // children
                    searchItems = GetElementsFromStateGraph(reference.ChildReference((StateUnit)e, false), ((StateUnit)e).nest?.graph, assetPath, searchContext, searchItems);
                } else if (((StateUnit)e).nest?.source == GraphSource.Macro)
                {
                    // just put this node, do not get inside it
                    if (isFoundElement(searchContext, e))
                    {
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e,
                            stateName = stateName
                        });
                    }
                }
            }
            else if (e is SubgraphUnit)
            {
                if (((SubgraphUnit)e).nest?.source == GraphSource.Embed && ((SubgraphUnit)e).nest?.graph?.elements.Count() > 0)
                {
                    //searchItems = GetElementsFromIGraph(reference, ((SubgraphUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                    // grab the current element
                    if (isFoundElement(searchContext, e))
                    {
                        //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e,
                            stateName = stateName
                        });
                    }
                    searchItems = GetElementsFromFlowGraph(reference.ChildReference(((SubgraphUnit)e), false), ((SubgraphUnit)e).nest.graph, assetPath, searchContext, searchItems);
                }
                else if (((SubgraphUnit)e).nest?.source == GraphSource.Macro)
                {
                    // just put this node, do not get inside it
                    if (isFoundElement(searchContext, e))
                    {
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(e)}",
                            assetPath = assetPath,
                            graphReference = reference,
                            graphGuid = e.guid.ToString(),
                            graphElement = e,
                            stateName = stateName
                        });
                    }
                }
            } 
            else
            {
                try
                {
                    if (isFoundElement(searchContext, e))
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
                    Debug.Log($"Could not add element {e?.guid} {assetPath} {reference?.graph?.title} because of {ex}");
                }
            }

            return searchItems;
        }

        private static bool isFoundElement(SearchContext searchContext, IGraphElement currentElement)
        {
            var currentElementName = GraphElement.GetElementName(currentElement);
            if (searchContext.isExactSearchTerm)
            {
                return currentElementName == searchContext.keyword && !IsIgnoredElement(currentElement);
            }
            return CleanString(currentElementName).Contains(CleanString(searchContext.keyword)) && !IsIgnoredElement(currentElement);
        }

        private static bool IsIgnoredElement(IGraphElement graphElement)
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
                if (i.graphGuid == item.graphGuid && i.assetPath == item.assetPath && i.stateName == item.stateName)
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

    public class SearchContext
    {
        public string keyword;
        public bool isExactSearchTerm;
    }
}
