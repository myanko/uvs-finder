using System.Collections.Generic;
using UnityEngine;
using System;
using Newtonsoft.Json;

namespace Unity.VisualScripting.UVSFinder
{
    public class Graph : IGraph
    {
        [JsonProperty(PropertyName = "elements")]
        public IEnumerable<GraphElement> graphElements;

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Prewarm()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> GetAotStubs(HashSet<object> visited)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<object> aotStubs { get; }

        public void OnBeforeSerialize()
        {
            throw new NotImplementedException();
        }

        public void OnAfterDeserialize()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<ISerializationDependency> deserializationDependencies { get; }

        public void OnAfterDependenciesDeserialized()
        {
            throw new NotImplementedException();
        }

        public Vector2 pan { get; set; }
        public float zoom { get; set; }

        [JsonProperty(PropertyName = "notElements")]
        public MergedGraphElementCollection elements { get; }

        public string title { get; }
        public string summary { get; }

        public IGraphData CreateData()
        {
            throw new NotImplementedException();
        }

        public IGraphDebugData CreateDebugData()
        {
            throw new NotImplementedException();
        }

        public void Instantiate(GraphReference instance)
        {
            throw new NotImplementedException();
        }

        public void Uninstantiate(GraphReference instance)
        {
            throw new NotImplementedException();
        }
    }
}
