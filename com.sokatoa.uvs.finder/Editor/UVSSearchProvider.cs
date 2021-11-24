using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.Serialization;
#if VS_YAML_RENAMED
using Unity.VisualScripting.YamlDotNet.RepresentationModel;
#else
using YamlDotNet.RepresentationModel;
#endif
using System.Linq;
using Newtonsoft.Json;
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
            try
            {
                var assetPath = AssetDatabase.GetAssetPath(graphWindow.reference.serializedObject);
                var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (assetType != typeof(StateGraphAsset))
                    assetType = typeof(ScriptGraphAsset);
                var itemsFound = FindNodesFromAssetPath(keyword, assetPath, assetType);
                if (itemsFound != null)
                {
                    return itemsFound;
                }
            } catch (Exception e)
            {
                Debug.Log($"encountered an error while searching in current script: {e}");
                return new List<ResultItem>();
            }

            return new List<ResultItem>();

        }

        // finds all the results nodes from the asset files
        // sets the searchItems with the nodes found as a GraphItem
        // for now, I can't seem to deserialize properly using the actual data classes
        // so I deserialize to my own classes instead
        // TODO:
        // - search for embeded scripts in scenes
        public static List<ResultItem> PerformSearchAll(string keyword)
        {
            var searchItems = new List<ResultItem>();
            try {
                string[] guids = AssetDatabase.FindAssets("t:ScriptGraphAsset");
                //Debug.Log($"found {guids.Length} script graph assets");
                foreach (string guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var itemsFound = FindNodesFromAssetPath(keyword, assetPath, typeof(ScriptGraphAsset));
                    if (itemsFound != null)
                    {
                        searchItems = searchItems.Concat(itemsFound).ToList();
                    }
                }
                // I can't seem to distinguish a stategraph from a scriptgraph from the json data
                guids = AssetDatabase.FindAssets("t:StateGraphAsset");
                //Debug.Log($"found {guids.Length} script graph assets");
                foreach (string guid in guids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var itemsFound = FindNodesFromAssetPath(keyword, assetPath, typeof(StateGraphAsset));
                    if (itemsFound != null)
                    {
                        searchItems = searchItems.Concat(itemsFound).ToList();
                    }
                }
            } catch(Exception e){
                Debug.Log("encountered an error while searching in all scripts");
            }
            return searchItems;
        }

        private static List<ResultItem> FindNodesFromAssetPath(string keyword, string assetPath, Type type)
        {
            try
            {
                var jsonString = DeserializeYamlAsset(assetPath);
                var graphAsset = CreateFromJSON(jsonString);
                
                if (graphAsset.graph?.graphElements?.Count() > 0)
                {
                    return FindNodesFromGraph(graphAsset, keyword, type, assetPath);
                }
            }
            catch (Exception e)
            {
                Debug.Log($"Error while deserializing the data of asset {assetPath}: {e}");   
            }
            //Debug.Log(assetPath + " - " + graphAsset.graph?.graphElements?.Count());


            return null;
        }

        // TODO: this method should eventually be replaced to use the actual data types
        // instead of mine when deserializing
        private static List<ResultItem> FindNodesFromGraph(GraphAsset graphAsset, string keyword, Type type, string assetPath = "")
        {
            var searchItems = new List<ResultItem>();
            //Debug.Log($"found {graphAsset.graph.graphElements.Count()} graph elements with keyword: {keyword}");
            foreach(var graphElement in graphAsset.graph.graphElements)
            {
                if (IsIgnoreElement(graphElement))
                    continue;

                if (string.IsNullOrEmpty(keyword))
                {// return all elements found
                    Debug.Log($"return {graphElement.GetElementName()} {assetPath}");
                    graphElement.graph = graphAsset.graphReference;
                    searchItems.Add(new ResultItem()
                    {
                        itemName = graphElement.GetElementName(),
                        assetPath = assetPath,
                        guid = graphElement.guid.ToString(),
                        graphElement = graphElement,
                        type = type
                    });
                }
                else
                {
                    var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
                    var elementNameLowerInvariant = graphElement.GetElementName().ToLowerInvariant();

                    // search in the elements directly
                    if (elementNameLowerInvariant.Contains(searchTermLowerInvariant))
                    {
                        graphElement.graph = graphAsset.graphReference;
                        searchItems.Add(new ResultItem()
                        {
                            itemName = graphElement.GetElementName(),
                            assetPath = assetPath,
                            guid = graphElement.guid.ToString(),
                            graphElement = graphElement,
                            type = type
                        });
                    }
                }

                // also search in embed graphs
                // TODO: make this recursive
                if (graphElement.nest?.embed?.graphElements != null)
                {
                    foreach(var embedGraphElement in graphElement.nest.embed.graphElements)
                    {
                        if (string.IsNullOrEmpty(keyword))
                        {
                            // return all elements found
                            searchItems.Add(new ResultItem()
                            {
                                itemName = embedGraphElement.GetElementName(),
                                assetPath = assetPath,
                                guid = embedGraphElement.guid.ToString(),
                                graphElement = embedGraphElement,
                                type = type
                            });
                        }
                        else
                        {
                            var searchTermLowerInvariant = keyword.ToLowerInvariant().Replace(" ", "").Replace(".", "");
                            var embedElementNameLowerInvariant = embedGraphElement.GetElementName().ToLowerInvariant();
                            if (embedElementNameLowerInvariant.Contains(searchTermLowerInvariant))
                            {
                                searchItems.Add(new ResultItem()
                                {
                                    itemName = embedGraphElement.GetElementName(),
                                    assetPath = assetPath,
                                    guid = embedGraphElement.guid.ToString(),
                                    graphElement = embedGraphElement,
                                    type = type
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
            switch (graphElement.type)
            {
                case "Bolt.ControlConnection":
                case "Bolt.ValueConnection":
                case "Unity.VisualScripting.ControlConnection":
                case "Unity.VisualScripting.ValueConnection":
                    return true;
            }

            return false;
        }

        private static GraphAsset CreateFromJSON(string jsonString)
        {
            return JsonConvert.DeserializeObject<GraphAsset>(jsonString, new Newtonsoft.Json.JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                NullValueHandling = NullValueHandling.Ignore,
            });
        }

        private static string DeserializeYamlAsset(string asset, string topNodeKey = "MonoBehaviour",
            string dataNodeKey = "_data")
        {
            Ensure.That(nameof(asset)).IsNotNull(asset);
            Ensure.That(nameof(topNodeKey)).IsNotNull(topNodeKey);
            Ensure.That(nameof(dataNodeKey)).IsNotNull(dataNodeKey);

            var assetPath = Path.Combine(Paths.project, asset);

            if (!File.Exists(assetPath))
            {
                throw new FileNotFoundException($"Asset file {assetPath} not found.", assetPath);
            }

            try
            {
                var input = new StreamReader(assetPath);
                var yaml = new YamlStream();
                yaml.Load(input);

                // Find the data node.
                var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
                var topNode = (YamlMappingNode)rootNode.Children[topNodeKey];
                var dataNode = (YamlMappingNode)topNode.Children[dataNodeKey];
                var jsonNode = (YamlScalarNode)dataNode.Children["_json"];
                var objectReferencesNode = (YamlSequenceNode)dataNode.Children["_objectReferences"];

                // Read the contents
                var jsonString = jsonNode.Value;
                return jsonString;
            }
            catch (Exception ex)
            {
                throw new SerializationException("Failed to deserialize YAML asset.", ex);
            }
        }
    }

    public class ResultItem{
        public string guid;
        public string itemName;
        public GraphElement graphElement;
        public string assetPath;
        public Type type; //The type of the node
        public string content; //the name of the event is in content
    }
}
