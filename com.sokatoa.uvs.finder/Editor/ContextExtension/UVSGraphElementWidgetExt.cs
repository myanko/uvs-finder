using HarmonyLib;
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

/// <summary>
/// This class is extending the right-click on nodes in the Visual Scripting Window
/// To achieve this, it relies on Harmony to be able to replace the contextOptions 
/// getter with my own implementation. I was not able to find another way of doing this...
/// </summary>
namespace Unity.VisualScripting.UVSFinder {

    [HarmonyPatch(typeof(IGraphElementWidget))]
    public class UVSGraphElementWidgetExt
    {
        // make sure DoPatching() is called at start either by
        // the mod loader or by your injector
        [InitializeOnLoadMethod]
        public static void DoPatching()
        {
            var harmony = new Harmony("com.sokatoa.patch");
            harmony.PatchAll();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GraphElementWidget<ICanvas, IGraphElement>), "contextOptions", MethodType.Getter)]
        public static IEnumerable<DropdownOption> Postfix1(IEnumerable<DropdownOption> __result, GraphElementWidget<ICanvas, IGraphElement> __instance) {
            // UnitWidget > NodeWidget > GraphElementWidget
            // SuperUnitWidget > NesterUnitWidget > UnitWidget
            var canvasProp = __instance.GetType().GetProperty("canvas", BindingFlags.NonPublic | BindingFlags.Instance);
            ICanvas canvas = (ICanvas)canvasProp.GetValue(__instance);

            var elementProp = __instance.GetType().GetProperty("element", BindingFlags.NonPublic | BindingFlags.Instance);

            IGraphElement element = (IGraphElement)elementProp?.GetValue(__instance);

            // --------------------
            // adding my own options
            var name = "";
            var type = "";
            
            if (__instance is Widget<ICanvas, IGraphElement>)
            {
                name = GraphElement.GetElementName(__instance.element);
                type = (__instance as IUnitWidget)?.unit.GetType().ToString();
                
            }
            else if (__instance is IWidget)
            {
                type = (__instance as IWidget).item.GetType().ToString();
                name = (__instance as IWidget).item.GetType().HumanName();
            }

            // adding the related searches per type
            switch (type)
            {
                case "Unity.VisualScripting.CustomEvent":
                case "Bolt.CustomEvent":
                    {
                        var curr = ((__instance as IUnitWidget).unit as CustomEvent);
                        var relatedFindName = $"{curr.defaultValues["name"]} [TriggerCustomEvent]";
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        break;
                    }
                case "Unity.VisualScripting.TriggerCustomEvent":
                case "Bolt.TriggerCustomEvent":
                    {

                        var curr = ((__instance as IUnitWidget).unit as TriggerCustomEvent);
                        var relatedFindName = $"{curr.defaultValues["name"]} [CustomEvent]";
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        break;
                    }
                case "Unity.VisualScripting.GetVariable":
                case "Bolt.GetVariable":
                    {
                        var curr = ((__instance as IUnitWidget).unit as GetVariable);
                        var relatedFindName = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
                        break;
                    }
                case "Unity.VisualScripting.SetVariable":
                case "Bolt.SetVariable":
                    {
                        var curr = ((__instance as IUnitWidget).unit as SetVariable);
                        var relatedFindName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
                        break;
                    }
                case "Unity.VisualScripting.IsVariableDefined":
                case "Bolt.IsVariableDefined":
                    {
                        var curr = ((__instance as IUnitWidget).unit as IsVariableDefined);
                        var relatedFindName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        break;
                    }
                case "Unity.VisualScripting.GetMember":
                    {
                        var curr = ((__instance as IUnitWidget).unit as GetMember);
                        var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Set {curr.member.name}";
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        break;
                    }
                case "Unity.VisualScripting.SetMember":
                    {
                        var curr = ((__instance as IUnitWidget).unit as SetMember);
                        var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Get {curr.member.name}";
                        yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                        yield return new DropdownOption((Action)(() => OnFindExact(name)), $"Find \"{name}\" ~");
                        break;
                    }
                default:
                    {
                        if (!String.IsNullOrEmpty(name))
                        {
                            yield return new DropdownOption((Action)(() => OnFind(name)), $"Find \"{name}\"");
                        }
                        break;
                    }
            }

            // --------------------
            // This is recreating the list returned by GraphElementWidget's get contextOptions
            var suffix = canvas?.selection.Count > 1 ? " Selection" : "";

            if (GraphClipboard.canCopySelection)
            {
                yield return new DropdownOption((Action)GraphClipboard.CopySelection, "Copy" + suffix);
                yield return new DropdownOption((Action)GraphClipboard.CutSelection, "Cut" + suffix);
            }

            if (GraphClipboard.canDuplicateSelection)
            {
                yield return new DropdownOption((Action)GraphClipboard.DuplicateSelection, "Duplicate" + suffix);
            }

            if (canvas.selection.Count > 0)
            {
                yield return new DropdownOption((Action)canvas.DeleteSelection, "Delete" + suffix);
            }

            if (element != null) // this is fishy that I have to add that and GraphElementWidget doesn't...
            {
                if (GraphClipboard.CanPasteInside(element))
                {
                    yield return new DropdownOption((Action)(() => GraphClipboard.PasteInside(element)), "Paste Inside");
                }
            }

            if (GraphClipboard.canPasteOutside)
            {
                yield return new DropdownOption((Action)(() => GraphClipboard.PasteOutside(true)), "Paste Outside");
            }

        }

        private static void OnFindExact(string keyword)
        {
            var uvsfinder = UVSFinder.GetUVSFinder();
            uvsfinder.OnSearchAction(keyword, true);
        }

        private static void OnFind(string keyword)
        {
            var uvsfinder = UVSFinder.GetUVSFinder();
            uvsfinder.OnSearchAction(keyword, false);
        }
    }
}
