using Newtonsoft.Json;

namespace Unity.VisualScripting.UVSFinder
{
    public class GraphValue
    {
        [JsonProperty(PropertyName = "$content")]
        public dynamic content;

        [JsonProperty(PropertyName = "$type")]
        public string type;
    }
}
