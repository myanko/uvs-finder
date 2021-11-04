using Newtonsoft.Json;

namespace Unity.VisualScripting.UVSFinder
{
    public class DefaultValues
    {
        public DefaultValue name;
    }

    public class DefaultValue
    {
        [JsonProperty(PropertyName = "$content")]
        public string content;
        [JsonProperty(PropertyName = "$type")]
        public string type;
    }
}
