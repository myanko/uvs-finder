using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.VisualScripting.UVSFinder
{
    public class Nest
    {
        public string source;
        public Embed embed;
    }

    public class Embed
    {
        public string title;
        public string summary;
        [JsonProperty(PropertyName = "elements")]
        public IEnumerable<GraphElement> graphElements;
    }
}
