using System;
using System.Linq;
using Unity.VisualScripting.UVSFinder.ExtensionMethods;

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
         
            name = GetNameFromSpecificTypes(ge);
            


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

        public static string GetNameFromSpecificTypes(IGraphElement ge)
        {
            switch (ge.GetType().ToString())
            {
                case "Unity.VisualScripting.GetMember":
                    return $"{((GetMember)ge).member.targetTypeName.Split('.').Last()} Get {((GetMember)ge).member.name}";
                case "Unity.VisualScripting.SetMember":
                    return $"{((SetMember)ge).member.targetTypeName.Split('.').Last()} Set {((SetMember)ge).member.name}";
                case "Unity.VisualScripting.InvokeMember":
                    {
                        if (((InvokeMember)ge).member.name == ".ctor")
                        {
                            return $"{((InvokeMember)ge).member.targetTypeName.Split('.').Last()} Create";
                        }
                        return $"{((InvokeMember)ge).member.targetTypeName.Split('.').Last()} {((InvokeMember)ge).member.name}";
                    }
                case "Unity.VisualScripting.GetVariable":
                case "Bolt.GetVariable":
                    return $"{((GetVariable)ge).defaultValues["name"]} [Get Variable: {((GetVariable)ge).kind}]";
                case "Unity.VisualScripting.IsVariableDefined":
                case "Bolt.IsVariableDefined":
                    return $"{((IsVariableDefined)ge).defaultValues["name"]} [Has Variable: {((IsVariableDefined)ge).kind}]";                    
                case "Unity.VisualScripting.SetVariable":
                case "Bolt.SetVariable":
                    return $"{((SetVariable)ge).defaultValues["name"]} [Set Variable: {((SetVariable)ge).kind}]";
                case "Unity.VisualScripting.BoltUnityEvent":
                case "Bolt.BoltUnityEvent":
                    var bue = ge as BoltUnityEvent;
                    return $"{bue.defaultValues["name"]} [BoltUnityEvent]";
                case "Unity.VisualScripting.BoltNamedAnimationEvent":
                case "Bolt.BoltNamedAnimationEvent":
                    var buae = ge as BoltNamedAnimationEvent;
                    return $"{buae.defaultValues["name"]} [BoltAnimationEvent]";
                case "Unity.VisualScripting.CustomEvent":
                case "Bolt.CustomEvent":
                    return $"{((CustomEvent)ge).defaultValues["name"]} [CustomEvent]";
                case "Unity.VisualScripting.TriggerCustomEvent":
                case "Bolt.TriggerCustomEvent":
                    return $"{((TriggerCustomEvent)ge).defaultValues["name"]} [TriggerCustomEvent]";
                case "Unity.VisualScripting.Literal":
                case "Bolt.Literal":
                    return $"{((Literal)ge).type.ToString().Split('.').Last()} \"{((Literal)ge).value}\" [Literal]";
                case "Unity.VisualScripting.GraphGroup":
                case "Bolt.GraphGroup":
                    return $"\"{((GraphGroup)ge).label}\" [Group]";
                case "Unity.VisualScripting.FlowState":
                    {
                        var flow = (FlowState)ge;
                        var name = "[FlowState]";

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
                        if (flow.nest.source == GraphSource.Embed)
                        {
                            name = name.Replace("[FlowState]", "[FlowState Embed]");
                        }
                        if (flow.isStart)
                        {
                            name = $"{name} [Start]";
                        }
                        return name;
                    }
                case "Unity.VisualScripting.FlowStateTransition":
                    {
                        var flow = (FlowStateTransition)ge;
                        var name = "[FlowStateTransition]";
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
                        return name;
                    }
                case "Unity.VisualScripting.AnyState":
                    {
                        var flow = (AnyState)ge;
                        var name  = "[AnyState]";
                        if (!string.IsNullOrEmpty(flow.graph.title))
                        {
                            name = $"{flow.graph.title} {name}";
                        }
                        return name;
                    }
#if SUBGRAPH_RENAME
                case "Unity.VisualScripting.SubgraphUnit":
                    {
                        var subgraph = (SubgraphUnit)ge;
                        var name = "";
                        if (subgraph.nest.source == GraphSource.Macro)
                        {
                            name = $"{subgraph.nest.macro.name} [SubGraph]";
                        } 
                        else
                        {
                            name = $"{subgraph.nest.embed.title} [SubGraph Embed]";
                        }
                        return name;
                    }
#else
                case "Unity.VisualScripting.SuperUnit":
                    {
                        var subgraph = (SuperUnit)ge;
                        var name = "";
                        if (subgraph.nest.source == GraphSource.Macro)
                        {
                            name = $"{subgraph.nest.macro.name} [SuperUnit]";
                        }
                        else
                        {
                            name = $"{subgraph.nest.embed.title} [SuperUnit Embed]";
                        }
                        return name;
                    }
#endif
                case "Unity.VisualScripting.StateUnit":
                    {
                        var stateUnit = (StateUnit)ge;
                        var name = "";
                        if (stateUnit.nest.source == GraphSource.Macro)
                        {
                            name = $"{stateUnit.nest.macro.name} [State]";
                        }
                        else
                        {
                            name = $"{stateUnit.nest.embed.title} [State Embed]";
                        }
                        return name;
                    }
                /*case "Bolt.SuperUnit":
                    {
                        var superUnit = (SuperUnit)ge;
                        if (nest.source == "Macro")
                        {
                            return $"{type.Split('.').Last()} [SubGraph]";
                        }
                        else
                        {
                            return $"{nest.embed.title} [SubGraph Embed]";
                        }
                    }*/
                default:
                    {
                        var name = BoltFlowNameUtility.UnitTitle(ge.GetType(), false, true);
                        if (name == "")
                        {
                            name = BoltFlowNameUtility.UnitTitle(ge.GetType(), true, true);
                        }
                        if (name == "")
                        {
                            //return ge.GetType().ToString();//.Split('.').Last();
                            return ge.GetType().HumanName();
                        }
                        return name;
                    }
            }
        }
    }
}

namespace Unity.VisualScripting.UVSFinder.ExtensionMethods
{
    public static class MyExtensions
    {
        public static bool HasMethod(this object objectToCheck, string methodName)
        {
            var type = objectToCheck.GetType();
            return type.GetMethod(methodName) != null;
        }

        public static bool HasMember(this object objectToCheck, string memberName)
        {
            var type = objectToCheck.GetType();
            return type.GetMember(memberName) != null;
        }
        public static bool HasProperty(this object objectToCheck, string propertyName)
        {
            var type = objectToCheck.GetType();
            return type.GetProperty(propertyName) != null;
        }
    }
}

