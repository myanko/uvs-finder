using System.Linq;

namespace Unity.VisualScripting.UVSFinder
{
    public class GraphElement
    {
        public GraphElement()
        {

        }

        public static string GetElementName(IGraphElement ge)
        {
            var name = "";

            switch (ge.GetType().ToString())
            {
                case "Unity.VisualScripting.GetMember":
                    name = $"{((GetMember)ge).member.targetTypeName.Split('.').Last()} Get {((GetMember)ge).member.name}";
                    break;
                case "Unity.VisualScripting.SetMember":
                    name = $"{((SetMember)ge).member.targetTypeName.Split('.').Last()} Set {((SetMember)ge).member.name}";
                    break;
                case "Unity.VisualScripting.InvokeMember":
                    name = $"{((InvokeMember)ge).member.targetTypeName.Split('.').Last()} {((InvokeMember)ge).member.name}";
                    break;
                case "Unity.VisualScripting.GetVariable":
                    name = $"{((GetVariable)ge).defaultValues["name"]} [Get Variable: {((GetVariable)ge).kind}]";
                    break;
                /*case "Bolt.GetVariable":
                    var t = new Bolt.GetVariable();
                    name = $"{t.name} [Get Variable: {t.kind}]";
                    //name = $"{defaultValues.name.content} [Get Variable: {kind}]";
                    break;*/
                case "Unity.VisualScripting.SetVariable":
                    name = $"{((SetVariable)ge).defaultValues["name"]} [Set Variable: {((SetVariable)ge).kind}]";
                    break;
                /*case "Bolt.SetVariable":
                    name = $"{defaultValues.name.content} [Set Variable: {kind}]";
                    break;*/
                case "Unity.VisualScripting.CustomEvent":
                    name = $"{((CustomEvent)ge).defaultValues["name"]} [CustomEvent]";
                    break;
                /*case "Bolt.CustomEvent":
                    name = $"{defaultValues.name.content} [CustomEvent]";
                    break;*/
                case "Unity.VisualScripting.TriggerCustomEvent":
                    name = $"{((TriggerCustomEvent)ge).defaultValues["name"]} [TriggerCustomEvent]";
                    break;
               /* case "Bolt.TriggerCustomEvent":
                    name = $"{defaultValues.name.content} [TriggerCustomEvent]";
                    break;*/
                case "Unity.VisualScripting.Literal":
                    name = $"{((Literal)ge).type.ToString().Split('.').Last()} {((Literal)ge).value} [Literal]";
                    break;
                /*case "Bolt.Literal":
                    name = $"{value.type.Split('.').Last()} \"{value.content}\" [Literal]";
                    break;*/
                case "Unity.VisualScripting.GraphGroup":
                    name = $"{((GraphGroup)ge).label} [Group]";
                    break;
                /*case "Bolt.GraphGroup":
                    name = $"\"{label}\" [Group]";
                    break;*/
                case "Unity.VisualScripting.FlowState":
                    {
                        var flow = (FlowState)ge;
                        name = "[FlowState]";

                        if (!string.IsNullOrEmpty(flow.graph.title))
                        {
                            name = $"{flow.graph.title} [FlowState]";
                        }
                        // this depends on the bread crumb and where I am...
                        // TODO: test with more than one level of pathing
                        if (!string.IsNullOrEmpty(flow.nest.graph.title))
                        {
                            name = $"{flow.nest.graph.title} [FlowState]";
                        }
                        if(flow.nest.source == GraphSource.Embed)
                        {
                            name = name.Replace("[FlowState]", "[FlowState Embed]");
                        }
                        if (flow.isStart)
                        {
                            name = $"{name} [Start]";
                        }
                        break;
                    }
                case "Unity.VisualScripting.FlowStateTransition":
                    {
                        var flow = (FlowStateTransition)ge;
                        name = "[FlowStateTransition]";
                        if (!string.IsNullOrEmpty(flow.graph.title))
                        {
                            name = $"{flow.graph.title} [FlowStateTransition]";
                        }
                        if (!string.IsNullOrEmpty(flow.nest.graph.title))
                        {
                            name = $"{flow.nest.graph.title} [FlowStateTransition]";
                        }
                        if (flow.nest.source == GraphSource.Embed)
                        {
                            name = name.Replace("[FlowStateTransition]", "[FlowStateTransition Embed]");
                        }
                        break;
                    }
                case "Unity.VisualScripting.AnyState":
                    {
                        var flow = (AnyState)ge;
                        name = "[AnyState]";
                        if (!string.IsNullOrEmpty(flow.graph.title))
                        {
                            name = $"{flow.graph.title} {name}";
                        }
                        break;
                    }

#if VISUAL_SCRIPTING_RENAME
                case "Unity.VisualScripting.SubgraphUnit":
                    {
                        var subgraph = (SubgraphUnit)ge;
                        if (subgraph.nest.source == GraphSource.Macro)
                        {
                            if (!string.IsNullOrEmpty(subgraph.graph?.title))
                            {
                                name = $"{subgraph.nest.macro.name} {subgraph.graph.title} [SubGraph]";
                            }
                            else
                            {
                                name = $"{subgraph.nest.macro.name} [SubGraph]";
                            }
                        } 
                        else
                        {
                            name = $"{subgraph.nest.embed.title} [SubGraph Embed]";
                        }
                        break;
                    }
#endif
                case "Unity.VisualScripting.StateUnit":
                    {
                        var stateUnit = (StateUnit)ge;
                        if (stateUnit.nest.source == GraphSource.Macro)
                        {
                            if (!string.IsNullOrEmpty(stateUnit.graph?.title))
                            {
                                name = $"{stateUnit.nest.macro.name} {stateUnit.graph.title} [State]";
                            }
                            else
                            {
                                name = $"{stateUnit.nest.macro.name} [State]";
                            }
                        }
                        else
                        {
                            name = $"{stateUnit.nest.embed.title} [State Embed]";
                        }
                        break;
                    }

                /*case "Bolt.SuperUnit":
                    {
                        if (nest.source == "Macro")
                        {
                            name = $"{type.Split('.').Last()} [SubGraph]";
                        }
                        else
                        {
                            name = $"{nest.embed.title} [SubGraph Embed]";
                        }
                        break;
                    }*/
                default:
                    name = ge.GetType().ToString().Split('.').Last();
                    break;
            }

            /*if (member != null)
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
            }*/

            /*if (nest != null && !string.IsNullOrEmpty(nest.embed?.title))
            {
                if (nest.source == "Macro")
                {
                    //TODO Find what script it is with the GUID
                    name = type.Split('.').Last();
                }
                else
                {
                    name = nest.embed.title;
                }
            }*/

            if (ge.GetType().ToString().StartsWith("Bolt.")) {
                name = $"Bolt {name}";
            }

            return name;
        }

        /*public string GetElementType()
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
        }*/
    }
}

