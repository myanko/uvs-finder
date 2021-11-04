using Newtonsoft.Json;
using System;
using System.Linq;

namespace Unity.VisualScripting.UVSFinder
{
    public class GraphElement
    {
        [JsonProperty(PropertyName = "$type")] 
        public string type;

        public VariableKind kind;

        public Nest nest;

        public GraphPosition position;
        public GraphReference graph { get; set; }
        public Guid guid { get; set; }

        public GraphMember member;
        public DefaultValues defaultValues;
        public GraphValue value;

        public string label;

        public string GetElementName()
        {
            var name = "";

            switch (type)
            {
                case "Unity.VisualScripting.GetVariable":
                case "Bolt.GetVariable":
                    name = $"{defaultValues.name.content} [Get Variable: {kind}]";
                    break;
                case "Unity.VisualScripting.SetVariable":
                case "Bolt.SetVariable":
                    name = $"{defaultValues.name.content} [Set Variable: {kind}]";
                    break;
                case "Unity.VisualScripting.CustomEvent":
                case "Bolt.CustomEvent":
                    name = $"{defaultValues.name.content} [CustomEvent]";
                    break;
                case "Unity.VisualScripting.TriggerCustomEvent":
                case "Bolt.TriggerCustomEvent":
                    name = $"{defaultValues.name.content} [TriggerCustomEvent]";
                    break;
                case "Unity.VisualScripting.Literal":
                case "Bolt.Literal":
                    name = $"{value.type.Split('.').Last()} \"{value.content}\" [Literal]";
                    break;
                case "Unity.VisualScripting.GraphGroup":
                case "Bolt.GraphGroup":
                    name = $"\"{label}\" [Group]";
                    break;
                case "Unity.VisualScripting.SubgraphUnit":
                case "Bolt.SuperUnit":
                    {
                        if (nest.source == "Macro")
                        {
                            //TBD Find what is the GUID
                            name = $"{type.Split('.').Last()} [SubGraph]";
                        }
                        else
                        {
                            name = $"{nest.embed.title} [SubGraph Embed]";
                        }
                        break;
                    }
                default:
                    name = type.Split('.').Last();
                    break;
            }

            if (member != null)
            {
                var memberName = member.name;
                if(member.name == ".ctor")
                {
                    memberName = "Create";
                }

                if (type.EndsWith("Member")) {
                    var cmd = type.Split('.').Last().Replace("Member", "");
                    name = $"{cmd} {member.targetType.Split('.').Last()} {memberName}";
                } else
                {
                    name = $"{member.targetType.Split('.').Last()} {memberName}";
                }
            }

            if (nest != null && !string.IsNullOrEmpty(nest.embed?.title))
            {
                if (nest.source == "Macro")
                {
                    //TBD Find what is the GUID
                    name = type.Split('.').Last();
                }
                else
                {
                    name = nest.embed.title;
                }
            }

            if (type.StartsWith("Bolt.")) {
                name = $"Bolt {name}";
            }

            return name;
        }

        public string GetElementType()
        {
            if (member != null)
            {
                return member.targetType;
            }

            if (type.EndsWith(".SetVariable") || type.EndsWith(".GetVariable")) {
                return defaultValues.name.type;
            }

            if (type == "Unity.VisualScripting.Literal")
            {
                return value.type;
            }

            return type;
        }
    }
}

