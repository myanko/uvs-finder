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
                List<ResultItem> itemsFound;
                if (assetType != typeof(StateGraphAsset))
                {
                    assetType = typeof(ScriptGraphAsset);
                    itemsFound = FindNodesFromScriptGraphAssetGuid(guid.ToString(), keyword);
                    if (itemsFound != null)
                    {
                        return itemsFound;
                    }
                }
                
                itemsFound = FindNodesFromScriptGraphAssetGuid(guid.ToString(), keyword);
                if (itemsFound != null)
                {
                    return itemsFound;
                }
            } catch (Exception e)
            {
                Debug.Log($"encountered an error while searching in current script: {e}");
            }

            return new List<ResultItem>();
        }

        // finds all the results nodes from the asset files
        // TODO:
        // - search for embeded scripts in scenes
        // - process the files async to speed up the lookup
        public static List<ResultItem> PerformSearchAll(string keyword)
        {
            var searchItems = new List<ResultItem>();
            try {
                string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
                Debug.Log($"found {guids.Length} script graph assets");
                foreach (string guid in guids)
                {
                    var itemsFound = FindNodesFromScriptGraphAssetGuid(guid, keyword);
                    if (itemsFound != null)
                    {
                        searchItems = searchItems.Concat(itemsFound).ToList();
                    }
                }
                // I can't seem to distinguish a stategraph from a scriptgraph from the json data
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                Debug.Log($"found {guids.Length} state graph assets");
                foreach (string guid in guids)
                {
                    var itemsFound = FindNodesFromStateGraphAssetGuid(guid, keyword);
                    if (itemsFound != null)
                    {
                        searchItems = searchItems.Concat(itemsFound).ToList();
                    }
                }
            } catch(Exception e){
                Debug.Log("encountered an error while searching in all scripts " + e.Message);
            }
            return searchItems;
        }

        // TODO: missing some recursion to dig into embedded elements
        private static List<ResultItem> FindNodesFromScriptGraphAssetGuid(string guid, string keyword)
        {
            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(assetPath);
            var searchItems = new List<ResultItem>();
            if (sga?.graph?.elements.Count() > 0)
            {
                foreach (var a in sga.graph.elements)
                {
                    var embedElementNameLowerInvariant = GraphElement.GetElementName(a).ToLowerInvariant();
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                    {
                        searchItems.Add(new ResultItem()
                        {
                            itemName = GraphElement.GetElementName(a),
                            assetPath = assetPath,
                            guid = a.guid.ToString(),
                            graphElement = a,
                            type = typeof(ScriptGraphAsset)
                        });
                    }
                    
                    // TODO: recurse somewhere here
                }
            }

            return searchItems;
        }

        // TODO: missing some recursion to dig into embedded elements
        private static List<ResultItem> FindNodesFromStateGraphAssetGuid(string guid, string keyword)
        {
            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(assetPath);
            var searchItems = new List<ResultItem>();
            // this picks up only the first "layer" of a state graph asset
            if (sga?.graph?.elements.Count() > 0)
            {
                Debug.Log($"stategraphasset {sga.name} has {sga.graph?.elements.Count()} elements");
                foreach (var a in sga.graph.elements)
                {
                    var embedElementNameLowerInvariant = GraphElement.GetElementName(a).ToLowerInvariant();
                    if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                    {
                        searchItems.Add(new ResultItem()
                        {
                            itemName = $"{GraphElement.GetElementName(a)}",
                            assetPath = assetPath,
                            guid = a.guid.ToString(),
                            graphElement = a,
                            type = typeof(StateGraphAsset)
                        });
                    }
                }
            }
            if (sga?.graph?.states.Count() > 0)
            {
                foreach (var state in sga.graph.states)
                {
                    if (state is INesterState && ((INesterState)state).childGraph?.elements.Count() > 0)
                    {
                        Debug.Log($"sga {sga.name} state {state.guid} {((INesterState)state).childGraph.title} has {((INesterState)state).childGraph.elements.Count()} elements");
                        foreach (var e in ((INesterState)state).childGraph?.elements)
                        {
                            var embedElementNameLowerInvariant = GraphElement.GetElementName(e).ToLowerInvariant();
                            if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                            {
                                searchItems.Add(new ResultItem()
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

    public class ResultItem{
        public string guid;
        public string itemName;
        public IGraphElement graphElement;
        public string assetPath;
        public Type type; //The type of the node
        public string content; //the name of the event is in content
    }
}
