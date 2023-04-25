using System;
using System.Linq;
using Unity.VisualScripting.UVSFinder.ExtensionMethods;
using UnityEngine;

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
            
            if (ge.GetType().ToString().StartsWith("Bolt.")) {
                name = $"Bolt {name}";
            }

            return name;
        }

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
                        else if (((InvokeMember)ge).member.targetTypeName == "UnityEngine.Animator")
                        {
                            return $"{((InvokeMember)ge).defaultValues["%name"]} [{((InvokeMember)ge).member.targetTypeName.Split('.').Last()}: {((InvokeMember)ge).member.name}]";
                        }
                        else if (((InvokeMember)ge).member.name == "GetKey")
                        {
                            try
                            {
                                return $"{((InvokeMember)ge).defaultValues["%key"]} [{((InvokeMember)ge).member.targetTypeName.Split('.').Last()}: {((InvokeMember)ge).member.name}]";
                            }
                            catch
                            {
                                return $"{((InvokeMember)ge).defaultValues["%name"]} [{((InvokeMember)ge).member.targetTypeName.Split('.').Last()}: {((InvokeMember)ge).member.name}]";
                            }
                        }
                        else if (((InvokeMember)ge).member.name == "GetButtonDown")
                        {
                            return $"{((InvokeMember)ge).defaultValues["%buttonName"]} [{((InvokeMember)ge).member.targetTypeName.Split('.').Last()}: {((InvokeMember)ge).member.name}]";
                        }
                        else if (((InvokeMember)ge).member.name == "GetAxis")
                        {
                            return $"{((InvokeMember)ge).defaultValues["%axisName"]} [{((InvokeMember)ge).member.targetTypeName.Split('.').Last()}: {((InvokeMember)ge).member.name}]";
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

                case "Unity.VisualScripting.InputSystem.OnInputSystemEventFloat":
                case "Bolt.InputSystem.OnInputSystemEventFloat":
                    return $"{((InputSystem.OnInputSystemEventFloat)ge).defaultValues["InputAction"]} [{ge.GetType().HumanName()}: {((InputSystem.OnInputSystemEventFloat)ge).InputActionChangeType}]";
                case "Unity.VisualScripting.InputSystem.OnInputSystemEventButton":
                case "Bolt.InputSystem.OnInputSystemEventButton":
                    return $"{((InputSystem.OnInputSystemEventButton)ge).defaultValues["InputAction"]} [{ge.GetType().HumanName()}: {((InputSystem.OnInputSystemEventButton)ge).InputActionChangeType}]";
                case "Unity.VisualScripting.InputSystem.OnInputSystemEventVector2":
                case "Bolt.InputSystem.OnInputSystemEventVector2":
                    return $"{((InputSystem.OnInputSystemEventVector2)ge).defaultValues["InputAction"]} [{ge.GetType().HumanName()}: {((InputSystem.OnInputSystemEventVector2)ge).InputActionChangeType}]";
                case "Unity.VisualScripting.OnMouseInput":
                case "Bolt.OnMouseInput":
                    return $"{((OnMouseInput)ge).defaultValues["button"]} [{ge.GetType().HumanName()}: {((OnMouseInput)ge).defaultValues["action"]}]";
                case "Unity.VisualScripting.OnKeyboardInput":
                case "Bolt.OnKeyboardInput":
                    return $"{((OnKeyboardInput)ge).defaultValues["key"]} [{ge.GetType().HumanName()}: {((OnKeyboardInput)ge).defaultValues["action"]}]";
                case "Unity.VisualScripting.OnButtonInput":
                case "Bolt.OnButtonInput":
                    return $"{((OnButtonInput)ge).defaultValues["buttonName"]} [{ge.GetType().HumanName()}: {((OnButtonInput)ge).defaultValues["action"]}]";
                case "Unity.VisualScripting.BoltUnityEvent":
                case "Bolt.BoltUnityEvent":
                    var bue = ge as BoltUnityEvent;
                    return $"{bue.defaultValues["name"]} [BoltUnityEvent]";
                case "Unity.VisualScripting.BoltNamedAnimationEvent":
                case "Bolt.BoltNamedAnimationEvent":
                    var buae = ge as BoltNamedAnimationEvent;
                    return $"{buae.defaultValues["name"]} [BoltAnimationEvent]";
                case "Unity.VisualScripting.Expose":
                case "Bolt.Expose":
                    return $"{(ge as Expose).type.ToString().Split('.').Last()} [Expose]";
                case "Unity.VisualScripting.OnCollisionEnter":
                case "Bolt.OnCollisionEnter":
                    return $"OnCollisionEnter [PhysicsEvent]";
                case "Unity.VisualScripting.OnCollisionExit":
                case "Bolt.OnCollisionExit":
                    return $"OnCollisionExit [PhysicsEvent]";
                case "Unity.VisualScripting.OnCollisionStay":
                case "Bolt.OnCollisionStay":
                    return $"OnCollisionStay [PhysicsEvent]";
                case "Unity.VisualScripting.OnJointBreak":
                case "Bolt.OnJointBreak":
                    return $"OnJointBreak [PhysicsEvent]";
                case "Unity.VisualScripting.OnControllerColliderHit":
                case "Bolt.OnControllerColliderHit":
                    return $"OnControllerColliderHit [PhysicsEvent]";
                case "Unity.VisualScripting.OnParticleCollision":
                case "Bolt.OnParticleCollision":
                    return $"OnParticleCollision [PhysicsEvent]";
                case "Unity.VisualScripting.OnTriggerEnter":
                case "Bolt.OnTriggerEnter":
                    return $"OnTriggerEnter [PhysicsEvent]";
                case "Unity.VisualScripting.OnTriggerExit":
                case "Bolt.OnTriggerExit":
                    return $"OnTriggerExit [PhysicsEvent]";
                case "Unity.VisualScripting.OnTriggerStay":
                case "Bolt.OnTriggerStay":
                    return $"OnTriggerStay [PhysicsEvent]";

                case "Unity.VisualScripting.CustomEvent":
                case "Bolt.CustomEvent":
                    return $"{((CustomEvent)ge).defaultValues["name"]} [CustomEvent]";
                case "Unity.VisualScripting.TriggerCustomEvent":
                case "Bolt.TriggerCustomEvent":
                    return $"{((TriggerCustomEvent)ge).defaultValues["name"]} [TriggerCustomEvent]";
                case "Unity.VisualScripting.Literal":
                case "Bolt.Literal":
                    var literalType = ((Literal)ge).type.ToString();
                    if (literalType == "System.Single")
                    {
                        return $"\"{((Literal)ge).value}\" {"Float"} [Literal]";
                    }
                    //Todo: Get the Layer value and convert it to a name
                    /*else if (literalType == "UnityEngine.LayerMask")
                    {
                        Debug.Log(((Literal)ge).value).;
                        //return $"{literalType.Split('.').Last()} \"{LayerMask.LayerToName((int)((Literal)ge).value)}\" [Literal]";
                    }*/
                    return $"\"{((Literal)ge).value}\" {literalType.Split('.').Last()} [Literal]";
                case "Unity.VisualScripting.GraphGroup":
                case "Bolt.GraphGroup":
                    return $"\"{((GraphGroup)ge).label}\" [Graph Group]";
                case "Unity.VisualScripting.FlowState":
                    {
                        var flow = (FlowState)ge;
                        var name = "[FlowState]";

                        if (!string.IsNullOrEmpty(flow.nest.nester.childGraph.title))
                        {
                            name = $"{flow.nest.graph.title} [FlowState]";
                        } else
                        {
                            name = $"Unnamed [FlowState]";
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
                        var transition = (FlowStateTransition)ge;
                        var name = "[FlowStateTransition]";
                        if (!string.IsNullOrEmpty(transition.nest.nester.childGraph.title))
                        {
                            name = $"{transition.nest.nester.childGraph.title} [FlowStateTransition]";
                        } else
                        {
                            name = $"Unnamed [FlowStateTransition]";
                        }
                        if (transition.nest.source == GraphSource.Embed)
                        {
                            name = name.Replace("[FlowStateTransition]", "[FlowStateTransition Embed]");
                        }
                        return name;
                    }
                case "Unity.VisualScripting.StateTransition":
                    {
                        var transition = (StateTransition)ge;
                        var name = "[StateTransition]";
                        if (!string.IsNullOrEmpty(transition.graph.title))
                        {
                            name = $"{transition.graph.title} [StateTransition]";
                        }
                        /*if (transition.source.g == GraphSource.Embed)
                        {
                            name = name.Replace("[FlowStateTransition]", "[FlowStateTransition Embed]");
                        }*/
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
                            name = String.IsNullOrEmpty(subgraph.nest.macro.name) ? "SuperUnit" : subgraph.nest.macro.name;
                            name = $"{name} [SuperUnit]";
                        }
                        else
                        {
                            name = String.IsNullOrEmpty(subgraph.nest.embed.title) ? "SuperUnit" : subgraph.nest.embed.title;
                            name = $"{name} [SuperUnit Embed]";
                        }
                        return name;
                    }
#endif
                case "Unity.VisualScripting.SuperState":
                    {
                        var subgraph = (SuperState)ge;
                        var name = "";
                        if (subgraph.nest.source == GraphSource.Macro)
                        {
                            name = String.IsNullOrEmpty(subgraph.nest.macro.name) ? "SuperState" : subgraph.nest.macro.name;
                            name = $"{name} [SuperState]";
                        }
                        else
                        {
                            name = String.IsNullOrEmpty(subgraph.nest.embed.title) ? "SuperState" : subgraph.nest.embed.title;
                            name = $"{name} [SuperState Embed]";
                        }
                        return name;
                    }
                case "Unity.VisualScripting.StateUnit":
                    {
                        var stateUnit = (StateUnit)ge;
                        var name = "";
                        if (stateUnit.nest.source == GraphSource.Macro)
                        {
                            if (!String.IsNullOrEmpty(stateUnit.nest.graph.title))
                            {
                                name = stateUnit.nest.graph.title;
                            } else if (!String.IsNullOrEmpty(stateUnit.nest.macro.name))
                            {
                                name = stateUnit.nest.macro.name;
                            } else
                            {
                                name = "State Unit";
                            }
                            name = $"{name} [Macro State]";
                        }
                        else
                        {
                            name = String.IsNullOrEmpty(stateUnit.nest.embed.title) ? "State Unit" : stateUnit.nest.embed.title;
                            name = $"{name} [State Embed]";
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

