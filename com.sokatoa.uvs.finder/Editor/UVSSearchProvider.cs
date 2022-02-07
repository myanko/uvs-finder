using System.Collections.Generic;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.VisualScripting.UVSFinder
{
    public class UVSSearchProvider
    {
        // finds all the results nodes from the current script opened
        // sets the searchItems with the nodes found as a GraphItem
        public static List<ResultItem> PerformSearchInCurrentScript(string keyword)
        {
            var graphWindow = EditorWindow.GetWindow<GraphWindow>();
            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            try
            {
                var assetPath = AssetDatabase.GetAssetPath(graphWindow.reference.serializedObject);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                var guid = AssetDatabase.GUIDFromAssetPath(assetPath);
                ResultItemList itemsFound = new ResultItemList();
                if (assetType != typeof(StateGraphAsset))
                {
                    assetType = typeof(ScriptGraphAsset);
                    itemsFound = FindNodesFromScriptGraphAssetGuid(guid.ToString(), keyword, itemsFound);
                    if (itemsFound != null)
                    {
                        return itemsFound.list;
                    }
                }
                
                itemsFound = FindNodesFromStateGraphAssetGuid(guid.ToString(), keyword, itemsFound);
                if (itemsFound != null)
                {
                    return itemsFound.list;
                }
            } catch (Exception e)
            {
                Debug.Log($"encountered an error while searching in current script: {e}");
            }

            return new List<ResultItem>();
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
                // I can't seem to distinguish a stategraph from a scriptgraph from the json data
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                //Debug.Log($"found {guids.Length} state graph assets");
                foreach (string guid in guids)
                {
                    searchItems = FindNodesFromStateGraphAssetGuid(guid, keyword, searchItems);
                }
            } catch(Exception e){
                Debug.Log($"encountered an error while searching in all scripts {e.Message} {e.StackTrace}");
            }

            return searchItems.list;
        }

        private static ResultItemList FindNodesFromScriptGraphAssetGuid(string guid, string keyword, ResultItemList searchItems)
        {
            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            if (sga?.graph?.elements.Count() > 0)
            {
                foreach (var a in sga.graph.elements)
                {
                    var embedElementNameLowerInvariant = GraphElement.GetElementName(a).ToLowerInvariant();
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                    {
                        searchItems.AddDistinct(new ResultItem()
                        {
                            itemName = GraphElement.GetElementName(a),
                            assetPath = assetPath,
                            guid = a.guid.ToString(),
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
            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
            // pick up the first layer's elements
            if (sga?.graph?.elements.Count() > 0)
            {
                //Debug.Log($"stategraphasset {sga.name} has {sga.graph?.elements.Count()} elements");
                GetElementsFromStateGraph(sga.graph, assetPath, searchTermLowerInvariant, searchItems);
            }
            
            return searchItems;
        }

        private static ResultItemList GetElementsFromIGraph(IGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            foreach (var a in graph.elements)
            {
                var embedElementNameLowerInvariant = GraphElement.GetElementName(a).ToLowerInvariant();
                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                {
                    searchItems.AddDistinct(new ResultItem()
                    {
                        itemName = $"{GraphElement.GetElementName(a)}",
                        assetPath = assetPath,
                        guid = a.guid.ToString(),
                        graphElement = a,
                        type = typeof(StateGraphAsset)
                    });
                }
            }
            return searchItems;
        }

        private static ResultItemList GetElementsFromStateGraph(StateGraph graph, string assetPath, string searchTermLowerInvariant, ResultItemList searchItems)
        {
            // get this layer's elements
            searchItems = GetElementsFromIGraph(graph, assetPath, searchTermLowerInvariant, searchItems);
            
            // get this layer's sublayers elements
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
                                    searchItems = GetElementsFromIGraph(((StateUnit)e).graph, assetPath, searchTermLowerInvariant, searchItems);
                                    searchItems = GetElementsFromIGraph(((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                                    searchItems = GetElementsFromStateGraph(((StateUnit)e).nest.graph, assetPath, searchTermLowerInvariant, searchItems);
                                }
                            }
                            else
                            {
                                var embedElementNameLowerInvariant = GraphElement.GetElementName(e).ToLowerInvariant();
                                if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                                {
                                    //Debug.Log($"Adding {GraphElement.GetElementName(e)} with state {state.graph.title} {state.guid} {((INesterState)state).childGraph?.title}");
                                    searchItems.AddDistinct(new ResultItem()
                                    {
                                        itemName = $"{GraphElement.GetElementName(e)}",
                                        assetPath = assetPath,
                                        guid = e.guid.ToString(),
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

        private static bool IsIgnoreElement(GraphElement graphElement)
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
    }

    // this is used to have a list of distinct items only
    // it might be covering the fact that I seem to search more than once on the same items
    // but let's go with this for now.
    public class ResultItemList
    {
        public List<ResultItem> list = new List<ResultItem>();
        public void AddDistinct(ResultItem item)
        {
            bool isInList = false;
            foreach (var i in list)
            {
                if (i.guid == item.guid)
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
        public string guid;
        public string itemName;
        public IGraphElement graphElement;
        public string assetPath;
        public Type type; //The type of the node
        public IGraphParentElement graphParentElement;
        public string content; //the name of the event is in content
    }
}
