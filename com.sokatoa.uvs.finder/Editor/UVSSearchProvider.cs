using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;
using Unity.VisualScripting;
using UnityObject = UnityEngine.Object;

#if !SUBGRAPH_RENAME
using SubgraphUnit = Unity.VisualScripting.SuperUnit;
using ScriptMachine = Unity.VisualScripting.FlowMachine;
#endif

// Note:
// StateMachine -> StateGraph -> FlowGraph/ScriptGraph
// ScriptMachine -> ScriptGraph
namespace Unity.VisualScripting.UVSFinder
{
    internal readonly struct UVSEventRenameInfo
    {
        public UVSEventRenameInfo(string valueText, string valueKey, string familyKey)
        {
            ValueText = valueText;
            ValueKey = valueKey;
            FamilyKey = familyKey;
        }

        public string ValueText { get; }
        public string ValueKey { get; }
        public string FamilyKey { get; }
        public bool IsValid => !string.IsNullOrEmpty(ValueText) && !string.IsNullOrEmpty(ValueKey) && !string.IsNullOrEmpty(FamilyKey);

        public bool Matches(UVSEventRenameInfo other)
        {
            return IsValid &&
                other.IsValid &&
                string.Equals(ValueKey, other.ValueKey, StringComparison.Ordinal) &&
                string.Equals(FamilyKey, other.FamilyKey, StringComparison.Ordinal) &&
                string.Equals(ValueText, other.ValueText, StringComparison.Ordinal);
        }
    }

    public class UVSSearchProvider
    {
        private const string CustomEventFamilyKey = "Unity.VisualScripting.CustomEvent";
        private static readonly string[] EventRenameValueKeys = { "name", "buttonName", "key", "button" };
        private static readonly FieldInfo variableDeclarationsCollectionField = typeof(VariableDeclarations).GetField("collection", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly PropertyInfo variableDeclarationNameProperty = typeof(VariableDeclaration).GetProperty(nameof(VariableDeclaration.name), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo variableDeclarationsChangedField = typeof(VariableDeclarations).GetField("OnVariableChanged", BindingFlags.Instance | BindingFlags.NonPublic);

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
                itemsFound = GrabReferenceVariableScopes(GraphWindow.active?.reference, "", searchContext, itemsFound);

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
            searchItems = GrabHierarchyVariableScopes(searchContext, searchItems);
            searchItems = GrabProjectVariableScopes(searchContext, searchItems);

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
                searchItems = GrabProjectVariableScopes(searchContext, searchItems);

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
                            searchItems = GrabPrefabVariableScopes(go, assetPath, searchContext, searchItems);

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
            if (sga?.graph != null)
            {
                searchItems = GrabGraphVariables(sga.GetReference().AsReference(), sga.graph, assetPath, searchContext, searchItems);

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
            if (canvas.graph == null)
            {
                return searchItems;
            }

            searchItems = GrabGraphVariables(GraphWindow.active?.reference, canvas.graph, "", searchContext, searchItems);

            if (canvas.graph.elements.Count == 0)
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
            
            if (scriptMachine == null)
            {
                return searchItems;
            }

            var reference = scriptMachine.GetReference().AsReference();
            searchItems = GrabGraphVariables(reference, scriptMachine.graph, assetPath, searchContext, searchItems, scriptMachine.gameObject);

            if (scriptMachine.graph.elements.Count() == 0 && scriptMachine.nest?.embed?.elements.Count == 0)
            {
                return searchItems;
            }

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
            if (graph == null)
            {
                return searchItems;
            }

            searchItems = GrabGraphVariables(reference, graph, assetPath, searchContext, searchItems);

            if (graph.elements.Count == 0)
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

        private static ResultItemList GrabReferenceVariableScopes(GraphReference reference, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            if (reference == null)
            {
                return searchItems;
            }

            try
            {
                if (reference.gameObject != null)
                {
                    searchItems = GrabObjectVariables(reference.gameObject, assetPath, "Object Blackboard", searchContext, searchItems);
                }
            }
            catch
            {
                // Stale graph references can throw while resolving their object.
            }

            try
            {
                if (reference.scene != null && Variables.ExistInScene(reference.scene))
                {
                    searchItems = GrabSceneVariables(reference.scene.Value, searchContext, searchItems);
                }
            }
            catch
            {
                // Stale graph references can throw while resolving their scene.
            }

            return GrabProjectVariableScopes(searchContext, searchItems);
        }

        private static ResultItemList GrabHierarchyVariableScopes(SearchContext searchContext, ResultItemList searchItems)
        {
            foreach (var variables in GameObject.FindObjectsOfType<Variables>())
            {
                if (variables == null || variables.GetComponent<SceneVariables>() != null)
                {
                    continue;
                }

                searchItems = GrabVariableDeclarations(
                    variables.declarations,
                    VariableKind.Object,
                    variables,
                    null,
                    variables.gameObject.scene.path,
                    "Object Blackboard",
                    variables.gameObject,
                    searchContext,
                    searchItems);
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.IsValid() && scene.isLoaded && SceneVariables.InstantiatedIn(scene))
                {
                    searchItems = GrabSceneVariables(scene, searchContext, searchItems);
                }
            }

            return searchItems;
        }

        private static ResultItemList GrabPrefabVariableScopes(GameObject prefabRoot, string assetPath, SearchContext searchContext, ResultItemList searchItems)
        {
            if (prefabRoot == null)
            {
                return searchItems;
            }

            foreach (var variables in prefabRoot.GetComponentsInChildren<Variables>(true))
            {
                if (variables == null || variables.GetComponent<SceneVariables>() != null)
                {
                    continue;
                }

                searchItems = GrabVariableDeclarations(
                    variables.declarations,
                    VariableKind.Object,
                    variables,
                    null,
                    assetPath,
                    "Object Blackboard",
                    variables.gameObject,
                    searchContext,
                    searchItems);
            }

            return searchItems;
        }

        private static ResultItemList GrabObjectVariables(GameObject gameObject, string assetPath, string stateName, SearchContext searchContext, ResultItemList searchItems)
        {
            if (gameObject == null || !Variables.ExistOnObject(gameObject))
            {
                return searchItems;
            }

            var variables = gameObject.GetComponent<Variables>();
            return GrabVariableDeclarations(variables.declarations, VariableKind.Object, variables, null, assetPath, stateName, gameObject, searchContext, searchItems);
        }

        private static ResultItemList GrabSceneVariables(Scene scene, SearchContext searchContext, ResultItemList searchItems)
        {
            if (!scene.IsValid() || !scene.isLoaded || !SceneVariables.InstantiatedIn(scene))
            {
                return searchItems;
            }

            var sceneVariables = SceneVariables.Instance(scene).variables;
            return GrabVariableDeclarations(sceneVariables.declarations, VariableKind.Scene, sceneVariables, null, scene.path, "Scene Blackboard", sceneVariables.gameObject, searchContext, searchItems);
        }

        private static ResultItemList GrabProjectVariableScopes(SearchContext searchContext, ResultItemList searchItems)
        {
            searchItems = GrabVariablesAsset(ApplicationVariables.asset, ApplicationVariables.assetPath, VariableKind.Application, "Application Blackboard", searchContext, searchItems);
            searchItems = GrabVariablesAsset(SavedVariables.asset, SavedVariables.assetPath, VariableKind.Saved, "Saved Blackboard", searchContext, searchItems);
            return searchItems;
        }

        private static ResultItemList GrabVariablesAsset(VariablesAsset asset, string fallbackAssetPath, VariableKind kind, string stateName, SearchContext searchContext, ResultItemList searchItems)
        {
            if (asset == null)
            {
                return searchItems;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                assetPath = fallbackAssetPath;
            }

            return GrabVariableDeclarations(asset.declarations, kind, asset, null, assetPath, stateName, null, searchContext, searchItems);
        }

        private static ResultItemList GrabGraphVariables(GraphReference reference, IGraph graph, string assetPath, SearchContext searchContext, ResultItemList searchItems, GameObject gameObject = null)
        {
            if (!(graph is IGraphWithVariables graphWithVariables))
            {
                return searchItems;
            }

            var owner = GetSerializedObject(reference);
            if (owner == null && !string.IsNullOrEmpty(assetPath))
            {
                owner = AssetDatabase.LoadMainAssetAtPath(assetPath);
            }

            if (owner == null)
            {
                owner = gameObject;
            }

            var stateName = string.IsNullOrEmpty(graph.title) ? "Graph Blackboard" : $"{graph.title} Blackboard";
            return GrabVariableDeclarations(graphWithVariables.variables, VariableKind.Graph, owner, reference, assetPath, stateName, gameObject, searchContext, searchItems);
        }

        private static ResultItemList GrabVariableDeclarations(
            VariableDeclarations declarations,
            VariableKind kind,
            UnityObject owner,
            GraphReference reference,
            string assetPath,
            string stateName,
            GameObject gameObject,
            SearchContext searchContext,
            ResultItemList searchItems)
        {
            if (declarations == null)
            {
                return searchItems;
            }

            foreach (var declaration in declarations)
            {
                if (!isFoundVariableDeclaration(searchContext, declaration, kind))
                {
                    continue;
                }

                searchItems.AddDistinct(new ResultItem()
                {
                    itemName = GetVariableDeclarationName(declaration, kind),
                    assetPath = assetPath,
                    graphReference = reference,
                    graphGuid = $"Variable:{kind}:{declaration.name}",
                    variableDeclaration = declaration,
                    variableDeclarations = declarations,
                    variableKind = kind,
                    editedObject = owner,
                    stateName = stateName,
                    gameObject = gameObject
                });
            }

            return searchItems;
        }

        private static bool isFoundVariableDeclaration(SearchContext searchContext, VariableDeclaration declaration, VariableKind kind)
        {
            if (declaration == null)
            {
                return false;
            }

            var declarationName = GetVariableDeclarationName(declaration, kind);
            if (searchContext.isExactSearchTerm)
            {
                return declaration.name == searchContext.keyword || declarationName == searchContext.keyword;
            }

            return CleanString(declaration.name).Contains(CleanString(searchContext.keyword)) || CleanString(declarationName).Contains(CleanString(searchContext.keyword));
        }

        internal static string GetVariableDeclarationName(VariableDeclaration declaration, VariableKind kind)
        {
            return GetVariableDeclarationName(declaration.name, kind);
        }

        internal static string GetVariableDeclarationName(string declarationName, VariableKind kind)
        {
            return $"{declarationName} [{kind} Variable]";
        }

        internal static bool TryRenameVariableDeclaration(VariableDeclarations declarations, VariableDeclaration declaration, string newName)
        {
            if (declarations == null || declaration == null || string.IsNullOrWhiteSpace(newName))
            {
                return false;
            }

            if (string.Equals(declaration.name, newName, StringComparison.Ordinal))
            {
                return false;
            }

            if (IsDuplicateVariableName(declarations, declaration, newName))
            {
                return false;
            }

            if (!(variableDeclarationsCollectionField?.GetValue(declarations) is VariableDeclarationCollection collection))
            {
                return false;
            }

            collection.EditorRename(declaration, newName);
            variableDeclarationNameProperty?.SetValue(declaration, newName);

            if (variableDeclarationsChangedField?.GetValue(declarations) is Action onVariableChanged)
            {
                onVariableChanged.Invoke();
            }

            return true;
        }

        internal static bool IsDuplicateVariableName(VariableDeclarations declarations, VariableDeclaration declaration, string newName)
        {
            if (declarations == null || declaration == null || string.IsNullOrWhiteSpace(newName))
            {
                return false;
            }

            return !string.Equals(declaration.name, newName, StringComparison.Ordinal) && declarations.IsDefined(newName);
        }

        internal static List<ResultItem> FilterVariableRenameResults(IEnumerable<ResultItem> items, string variableName, VariableKind kind)
        {
            if (items == null || string.IsNullOrEmpty(variableName))
            {
                return new List<ResultItem>();
            }

            return items
                .Where(item => IsVariableRenameResult(item, variableName, kind))
                .ToList();
        }

        internal static List<ResultItem> FilterEventRenameResults(IEnumerable<ResultItem> items, UVSEventRenameInfo eventInfo)
        {
            if (items == null || !eventInfo.IsValid)
            {
                return new List<ResultItem>();
            }

            return items
                .Where(item => IsEventRenameResult(item, eventInfo))
                .ToList();
        }

        internal static bool IsVariableRenameResult(ResultItem item, string variableName, VariableKind kind)
        {
            if (item == null || string.IsNullOrEmpty(variableName))
            {
                return false;
            }

            if (item.variableDeclaration != null)
            {
                return item.variableKind == kind && string.Equals(item.variableDeclaration.name, variableName, StringComparison.Ordinal);
            }

            return TryGetVariableUnitInfo(item.graphElement, out var unitName, out var unitKind) &&
                unitKind == kind &&
                string.Equals(unitName, variableName, StringComparison.Ordinal);
        }

        internal static bool IsEventRenameResult(ResultItem item, UVSEventRenameInfo eventInfo)
        {
            return item != null &&
                eventInfo.IsValid &&
                TryGetEventRenameInfo(item.graphElement, out var unitInfo) &&
                eventInfo.Matches(unitInfo);
        }

        internal static bool TryGetVariableUnitInfo(IGraphElement element, out string variableName, out VariableKind kind)
        {
            variableName = null;
            kind = default;

            switch (element)
            {
                case GetVariable getVariable:
                    kind = getVariable.kind;
                    return TryGetDefaultVariableName(getVariable, out variableName);
                case SetVariable setVariable:
                    kind = setVariable.kind;
                    return TryGetDefaultVariableName(setVariable, out variableName);
                case IsVariableDefined isVariableDefined:
                    kind = isVariableDefined.kind;
                    return TryGetDefaultVariableName(isVariableDefined, out variableName);
            }

            return false;
        }

        internal static bool TryGetEventRenameInfo(IGraphElement element, out UVSEventRenameInfo eventInfo)
        {
            eventInfo = default;

            switch (element)
            {
                case CustomEvent customEvent:
                    return TryCreateEventRenameInfo(customEvent, "name", CustomEventFamilyKey, out eventInfo);
                case TriggerCustomEvent triggerCustomEvent:
                    return TryCreateEventRenameInfo(triggerCustomEvent, "name", CustomEventFamilyKey, out eventInfo);
            }

            if (!(element is IEventUnit) || !(element is IUnit unit))
            {
                return false;
            }

            foreach (var valueKey in EventRenameValueKeys)
            {
                if (TryCreateEventRenameInfo(unit, valueKey, GetEventFamilyKey(element, valueKey), out eventInfo))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetDefaultVariableName(IUnit unit, out string variableName)
        {
            return TryGetDefaultName(unit, out variableName);
        }

        private static bool TryGetDefaultName(IUnit unit, out string defaultName)
        {
            defaultName = null;

            if (unit == null || !unit.defaultValues.TryGetValue("name", out var value))
            {
                return false;
            }

            defaultName = Convert.ToString(value, CultureInfo.InvariantCulture);
            return !string.IsNullOrEmpty(defaultName);
        }

        private static bool TryCreateEventRenameInfo(IUnit unit, string valueKey, string familyKey, out UVSEventRenameInfo eventInfo)
        {
            eventInfo = default;

            if (unit == null || string.IsNullOrEmpty(valueKey) || string.IsNullOrEmpty(familyKey) || !unit.defaultValues.TryGetValue(valueKey, out var value))
            {
                return false;
            }

            var valueText = Convert.ToString(value, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(valueText))
            {
                return false;
            }

            eventInfo = new UVSEventRenameInfo(valueText, valueKey, familyKey);
            return true;
        }

        private static string GetEventFamilyKey(IGraphElement element, string valueKey)
        {
            var type = element?.GetType();
            return type == null ? valueKey : $"{type.FullName}:{valueKey}";
        }

        private static UnityObject GetSerializedObject(GraphReference reference)
        {
            try
            {
                return reference?.serializedObject;
            }
            catch
            {
                return null;
            }
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
            if (keyword == null)
            {
                return string.Empty;
            }

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
                if (item.variableDeclaration != null || i.variableDeclaration != null)
                {
                    if (ReferenceEquals(i.variableDeclaration, item.variableDeclaration) && ReferenceEquals(i.editedObject, item.editedObject))
                    {
                        isInList = true;
                    }
                }
                else if (i.graphGuid == item.graphGuid && i.assetPath == item.assetPath && i.stateName == item.stateName)
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
        public VariableDeclaration variableDeclaration;
        public string assetPath;
        public GraphReference graphReference;
        public GameObject gameObject;
        public string stateName;
        internal VariableDeclarations variableDeclarations;
        internal VariableKind? variableKind;
        internal UnityObject editedObject;
        internal UVSReplacePreview replacePreview;
    }

    public class SearchContext
    {
        public string keyword;
        public bool isExactSearchTerm;
    }

    internal enum UVSReplaceMode
    {
        Values,
        Node
    }

    internal sealed class UVSReplacePreview
    {
        public string beforeName;
        public string afterName;
        public int changeCount;
        public bool canReplace;
        public string message;

        public string Summary
        {
            get
            {
                if (!string.IsNullOrEmpty(message))
                {
                    return message;
                }

                if (!canReplace)
                {
                    return "No editable value found";
                }

                return $"{beforeName} -> {afterName}";
            }
        }
    }

    internal static class UVSReplaceProvider
    {
        public static UVSReplacePreview PreviewValueReplacement(ResultItem item, string find, string replace, bool isExact)
        {
            if (item?.variableDeclaration != null)
            {
                return PreviewVariableDeclarationRename(item, find, replace, isExact);
            }

            if (item?.graphElement == null)
            {
                return Message("No graph element selected.");
            }

            if (string.IsNullOrEmpty(find))
            {
                return Message("Type text to find before replacing values.");
            }

            var edits = CollectValueEdits(item.graphElement, find, replace, isExact);
            var beforeName = GraphElement.GetElementName(item.graphElement);

            if (edits.Count == 0)
            {
                return new UVSReplacePreview
                {
                    beforeName = beforeName,
                    afterName = beforeName,
                    canReplace = false,
                    changeCount = 0
                };
            }

            ApplyEdits(edits);
            var afterName = GraphElement.GetElementName(item.graphElement);
            RevertEdits(edits);

            return new UVSReplacePreview
            {
                beforeName = beforeName,
                afterName = afterName,
                changeCount = edits.Count,
                canReplace = true
            };
        }

        public static int ReplaceValues(ResultItem item, string find, string replace, bool isExact)
        {
            if (item?.variableDeclaration != null)
            {
                return RenameVariableDeclaration(item, find, replace, isExact);
            }

            if (item?.graphElement == null || string.IsNullOrEmpty(find))
            {
                return 0;
            }

            var edits = CollectValueEdits(item.graphElement, find, replace, isExact);
            if (edits.Count == 0)
            {
                return 0;
            }

            RecordEditedObject(item, "Find And Replace Values");
            ApplyEdits(edits);
            MarkEditedObjectDirty(item);

            item.itemName = GraphElement.GetElementName(item.graphElement);
            item.replacePreview = null;

            return edits.Count;
        }

        public static void WithValuePreview(ResultItem item, string find, string replace, bool isExact, Action drawPreview)
        {
            if (item?.variableDeclaration != null)
            {
                drawPreview?.Invoke();
                return;
            }

            if (item?.graphElement == null || drawPreview == null || string.IsNullOrEmpty(find))
            {
                drawPreview?.Invoke();
                return;
            }

            var edits = CollectValueEdits(item.graphElement, find, replace, isExact);
            if (edits.Count == 0)
            {
                drawPreview();
                return;
            }

            ApplyEdits(edits);
            try
            {
                drawPreview();
            }
            finally
            {
                RevertEdits(edits);
            }
        }

        private static UVSReplacePreview PreviewVariableDeclarationRename(ResultItem item, string find, string replace, bool isExact)
        {
            if (string.IsNullOrEmpty(find))
            {
                return Message("Type text to find before renaming variables.");
            }

            if (!TryCreateVariableRename(item, find, replace, isExact, out var newName))
            {
                return new UVSReplacePreview
                {
                    beforeName = item.itemName,
                    afterName = item.itemName,
                    canReplace = false,
                    changeCount = 0
                };
            }

            if (string.IsNullOrWhiteSpace(newName))
            {
                return Message("Variable names cannot be empty.");
            }

            if (UVSSearchProvider.IsDuplicateVariableName(item.variableDeclarations, item.variableDeclaration, newName))
            {
                return Message($"Variable already exists: {newName}");
            }

            return new UVSReplacePreview
            {
                beforeName = item.itemName,
                afterName = UVSSearchProvider.GetVariableDeclarationName(newName, item.variableKind ?? VariableKind.Graph),
                changeCount = 1,
                canReplace = true
            };
        }

        private static int RenameVariableDeclaration(ResultItem item, string find, string replace, bool isExact)
        {
            if (item?.variableDeclaration == null || string.IsNullOrEmpty(find))
            {
                return 0;
            }

            if (!TryCreateVariableRename(item, find, replace, isExact, out var newName))
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(newName) || UVSSearchProvider.IsDuplicateVariableName(item.variableDeclarations, item.variableDeclaration, newName))
            {
                return 0;
            }

            RecordEditedObject(item, "Find And Rename Variable");
            if (!UVSSearchProvider.TryRenameVariableDeclaration(item.variableDeclarations, item.variableDeclaration, newName))
            {
                return 0;
            }

            MarkEditedObjectDirty(item);

            item.graphGuid = $"Variable:{item.variableKind ?? VariableKind.Graph}:{newName}";
            item.itemName = UVSSearchProvider.GetVariableDeclarationName(item.variableDeclaration, item.variableKind ?? VariableKind.Graph);
            item.replacePreview = null;

            return 1;
        }

        private static bool TryCreateVariableRename(ResultItem item, string find, string replace, bool isExact, out string newName)
        {
            newName = item?.variableDeclaration?.name;

            if (item?.variableDeclaration == null)
            {
                return false;
            }

            if (isExact && string.Equals(item.itemName, find, StringComparison.OrdinalIgnoreCase))
            {
                newName = replace;
                return !string.Equals(item.variableDeclaration.name, newName, StringComparison.Ordinal);
            }

            return TryReplaceString(item.variableDeclaration.name, find, replace, isExact, out newName);
        }

        public static UVSReplacePreview PreviewNodeReplacement(ResultItem item, IUnit replacementTemplate)
        {
            if (item?.graphElement == null)
            {
                return Message("No graph element selected.");
            }

            if (replacementTemplate == null)
            {
                return Message("Pick a replacement node from the current graph.");
            }

            if (!(item.graphElement is IUnit))
            {
                return Message("Only flow nodes can be replaced by a node template.");
            }

            return new UVSReplacePreview
            {
                beforeName = GraphElement.GetElementName(item.graphElement),
                afterName = GraphElement.GetElementName((IGraphElement)replacementTemplate),
                changeCount = 1,
                canReplace = true
            };
        }

        public static int ReplaceNode(ResultItem item, IUnit replacementTemplate)
        {
            if (!(item?.graphElement is IUnit oldUnit) || replacementTemplate == null || oldUnit.graph == null)
            {
                return 0;
            }

            var graph = oldUnit.graph;
            var position = oldUnit.position;
            var preservation = UnitPreservation.Preserve(oldUnit);
            var newUnit = replacementTemplate.CloneViaSerialization();
            var templateDefaults = newUnit.defaultValues.ToDictionary(pair => pair.Key, pair => pair.Value);

            RecordEditedObject(item, "Find And Replace Node");

            graph.units.Remove(oldUnit);
            newUnit.guid = Guid.NewGuid();
            newUnit.position = position;
            graph.units.Add(newUnit);

            preservation.RestoreTo(newUnit);
            RestoreTemplateDefaultValues(newUnit, templateDefaults);

            MarkEditedObjectDirty(item);

            item.graphElement = newUnit;
            item.graphGuid = newUnit.guid.ToString();
            item.itemName = GraphElement.GetElementName(newUnit);
            item.replacePreview = null;

            return 1;
        }

        private static UVSReplacePreview Message(string message)
        {
            return new UVSReplacePreview
            {
                canReplace = false,
                changeCount = 0,
                message = message
            };
        }

        private static void RestoreTemplateDefaultValues(IUnit unit, Dictionary<string, object> templateDefaults)
        {
            foreach (var pair in templateDefaults)
            {
                if (!unit.defaultValues.ContainsKey(pair.Key) || !unit.valueInputs.Contains(pair.Key))
                {
                    continue;
                }

                var input = unit.valueInputs[pair.Key];
                if (pair.Value == null || input.type.IsInstanceOfType(pair.Value) || input.type.IsAssignableFrom(pair.Value.GetType()))
                {
                    unit.defaultValues[pair.Key] = pair.Value;
                }
            }
        }

        private static List<ValueEdit> CollectValueEdits(IGraphElement element, string find, string replace, bool isExact)
        {
            var edits = new List<ValueEdit>();

            if (element is IUnit unit)
            {
                foreach (var key in unit.defaultValues.Keys.ToArray())
                {
                    var oldValue = unit.defaultValues[key];
                    if (TryCreateReplacement(oldValue, find, replace, isExact, out var newValue))
                    {
                        edits.Add(new ValueEdit(oldValue, newValue, value => unit.defaultValues[key] = value));
                    }
                }
            }

            if (element is Literal literal && TryCreateReplacement(literal.value, find, replace, isExact, out var literalValue))
            {
                edits.Add(new ValueEdit(literal.value, literalValue, value => literal.value = value));
            }

            if (element is GraphGroup group)
            {
                AddStringEdit(edits, group.label, find, replace, isExact, value => group.label = value);
                AddStringEdit(edits, group.comment, find, replace, isExact, value => group.comment = value);
            }

            AddChildGraphEdits(edits, element, find, replace, isExact);

            return edits;
        }

        private static void AddChildGraphEdits(List<ValueEdit> edits, IGraphElement element, string find, string replace, bool isExact)
        {
            IGraph childGraph = null;

            if (element is FlowState flowState && flowState.nest.source == GraphSource.Embed)
            {
                childGraph = flowState.nest.embed;
            }
            else if (element is FlowStateTransition flowStateTransition && flowStateTransition.nest.source == GraphSource.Embed)
            {
                childGraph = flowStateTransition.nest.embed;
            }
            else if (element is SuperState superState && superState.nest.source == GraphSource.Embed)
            {
                childGraph = superState.nest.embed;
            }
            else if (element is StateUnit stateUnit && stateUnit.nest.source == GraphSource.Embed)
            {
                childGraph = stateUnit.nest.embed;
            }
            else if (element is SubgraphUnit subgraphUnit && subgraphUnit.nest.source == GraphSource.Embed)
            {
                childGraph = subgraphUnit.nest.embed;
            }

            if (!(childGraph is Graph editableGraph))
            {
                return;
            }

            AddStringEdit(edits, editableGraph.title, find, replace, isExact, value => editableGraph.title = value);
            AddStringEdit(edits, editableGraph.summary, find, replace, isExact, value => editableGraph.summary = value);
        }

        private static void AddStringEdit(List<ValueEdit> edits, string oldValue, string find, string replace, bool isExact, Action<string> setter)
        {
            if (!TryReplaceString(oldValue, find, replace, isExact, out var newValue))
            {
                return;
            }

            edits.Add(new ValueEdit(oldValue, newValue, value => setter((string)value)));
        }

        private static bool TryCreateReplacement(object oldValue, string find, string replace, bool isExact, out object newValue)
        {
            newValue = null;

            if (oldValue == null)
            {
                return false;
            }

            if (oldValue is string oldString)
            {
                if (!TryReplaceString(oldString, find, replace, isExact, out var newString))
                {
                    return false;
                }

                newValue = newString;
                return true;
            }

            if (!isExact)
            {
                return false;
            }

            if (!string.Equals(Convert.ToString(oldValue, CultureInfo.InvariantCulture), find, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var valueType = oldValue.GetType();

            try
            {
                if (valueType.IsEnum)
                {
                    newValue = Enum.Parse(valueType, replace, true);
                    return true;
                }

                if (valueType == typeof(bool))
                {
                    newValue = bool.Parse(replace);
                    return true;
                }

                if (valueType == typeof(int))
                {
                    newValue = int.Parse(replace, CultureInfo.InvariantCulture);
                    return true;
                }

                if (valueType == typeof(float))
                {
                    newValue = float.Parse(replace, CultureInfo.InvariantCulture);
                    return true;
                }

                if (valueType == typeof(double))
                {
                    newValue = double.Parse(replace, CultureInfo.InvariantCulture);
                    return true;
                }

                if (valueType == typeof(long))
                {
                    newValue = long.Parse(replace, CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryReplaceString(string oldValue, string find, string replace, bool isExact, out string newValue)
        {
            newValue = oldValue;

            if (oldValue == null || string.IsNullOrEmpty(find))
            {
                return false;
            }

            if (isExact)
            {
                if (!string.Equals(oldValue, find, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                newValue = replace;
                return newValue != oldValue;
            }

            newValue = ReplaceOrdinalIgnoreCase(oldValue, find, replace ?? string.Empty);
            return newValue != oldValue;
        }

        private static string ReplaceOrdinalIgnoreCase(string input, string find, string replace)
        {
            var index = input.IndexOf(find, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return input;
            }

            var result = string.Empty;
            var currentIndex = 0;

            while (index >= 0)
            {
                result += input.Substring(currentIndex, index - currentIndex);
                result += replace;
                currentIndex = index + find.Length;
                index = input.IndexOf(find, currentIndex, StringComparison.OrdinalIgnoreCase);
            }

            result += input.Substring(currentIndex);
            return result;
        }

        private static void ApplyEdits(IEnumerable<ValueEdit> edits)
        {
            foreach (var edit in edits)
            {
                edit.Apply();
            }
        }

        private static void RevertEdits(IEnumerable<ValueEdit> edits)
        {
            foreach (var edit in edits.Reverse())
            {
                edit.Revert();
            }
        }

        private static UnityObject GetEditedObject(ResultItem item)
        {
            if (item?.editedObject != null)
            {
                return item.editedObject;
            }

            try
            {
                if (item?.graphReference?.serializedObject != null)
                {
                    return item.graphReference.serializedObject;
                }
            }
            catch
            {
                // Some stale graph references throw while resolving serializedObject.
            }

            if (!string.IsNullOrEmpty(item?.assetPath))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(item.assetPath);
                if (asset != null)
                {
                    return asset;
                }
            }

            return item?.gameObject;
        }

        private static void RecordEditedObject(ResultItem item, string name)
        {
            var editedObject = GetEditedObject(item);
            if (editedObject == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(editedObject, name);
        }

        private static void MarkEditedObjectDirty(ResultItem item)
        {
            var editedObject = GetEditedObject(item);
            if (editedObject == null)
            {
                return;
            }

            if (!editedObject.IsSceneBound())
            {
                EditorUtility.SetDirty(editedObject);
            }

            if (editedObject.IsPrefabInstance())
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(editedObject);
            }

            var scene = GetScene(editedObject);
            if (scene.HasValue && scene.Value.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(scene.Value);
            }
        }

        private static Scene? GetScene(UnityObject editedObject)
        {
            if (editedObject is GameObject gameObject)
            {
                return gameObject.scene;
            }

            if (editedObject is Component component)
            {
                return component.gameObject.scene;
            }

            return null;
        }

        private sealed class ValueEdit
        {
            private readonly object oldValue;
            private readonly object newValue;
            private readonly Action<object> setter;

            public ValueEdit(object oldValue, object newValue, Action<object> setter)
            {
                this.oldValue = oldValue;
                this.newValue = newValue;
                this.setter = setter;
            }

            public void Apply()
            {
                setter(newValue);
            }

            public void Revert()
            {
                setter(oldValue);
            }
        }
    }
}
