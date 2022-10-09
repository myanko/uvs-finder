using HarmonyLib;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using System;

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
        static IEnumerable<DropdownOption> Postfix1(IEnumerable<DropdownOption> __result, GraphElementWidget<ICanvas, IGraphElement> __instance){ 
            // UnitWidget > NodeWidget > GraphElementWidget
            // SuperUnitWidget > NesterUnitWidget > UnitWidget
            var canvasProp = __instance.GetType().GetProperty("canvas", BindingFlags.NonPublic | BindingFlags.Instance);
            ICanvas canvas = (ICanvas)canvasProp.GetValue(__instance);

            var elementProp = __instance.GetType().GetProperty("element", BindingFlags.NonPublic | BindingFlags.Instance);
            
            IGraphElement element = (IGraphElement)elementProp?.GetValue(__instance);

            // --------------------
            // adding my own options
            // TODO: generalize this...
            if ((__instance as IUnitWidget)?.unit is CustomEvent)
            {
                var curr = ((__instance as IUnitWidget).unit as CustomEvent);
                var findName = $"{curr.defaultValues["name"]} [CustomEvent]";
                var relatedFindName = $"{curr.defaultValues["name"]} [TriggerCustomEvent]";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
            }
            if ((__instance as IUnitWidget)?.unit is TriggerCustomEvent)
            {
                var curr = ((__instance as IUnitWidget).unit as TriggerCustomEvent);
                var findName = $"{curr.defaultValues["name"]} [TriggerCustomEvent]";
                var relatedFindName = $"{curr.defaultValues["name"]} [CustomEvent]";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
            }

            if ((__instance as IUnitWidget)?.unit is GetVariable)
            {
                var curr = ((__instance as IUnitWidget).unit as GetVariable);
                var findName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                var relatedFindName = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
            }
            if ((__instance as IUnitWidget)?.unit is SetVariable)
            {
                var curr = ((__instance as IUnitWidget).unit as SetVariable);
                var findName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                var relatedFindName = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
            }
            if ((__instance as IUnitWidget)?.unit is IsVariableDefined)
            {
                var curr = ((__instance as IUnitWidget).unit as IsVariableDefined);
                var findName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                var relatedFindName = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
            }

            if ((__instance as IUnitWidget)?.unit is GetMember)
            {
                var curr = ((__instance as IUnitWidget).unit as GetMember);
                var findName = $"{curr.member.targetTypeName.Split('.').Last()} Get {curr.member.name}";
                var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Set {curr.member.name}";
                var relatedFindName2 = $"{curr.member.targetTypeName.Split('.').Last()} {curr.member.name}";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
            }
            if ((__instance as IUnitWidget)?.unit is SetMember)
            {
                var curr = ((__instance as IUnitWidget).unit as SetMember);
                var findName = $"{curr.member.targetTypeName.Split('.').Last()} Get {curr.member.name}";
                var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Set {curr.member.name}";
                var relatedFindName2 = $"{curr.member.targetTypeName.Split('.').Last()} {curr.member.name}";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
            }
            if ((__instance as IUnitWidget)?.unit is InvokeMember)
            {
                var curr = ((__instance as IUnitWidget).unit as InvokeMember);
                var findName = $"{curr.member.targetTypeName.Split('.').Last()} Get {curr.member.name}";
                var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Set {curr.member.name}";
                var relatedFindName2 = $"{curr.member.targetTypeName.Split('.').Last()} {curr.member.name}";
                yield return new DropdownOption((Action)(() => OnFindExact(findName)), $"Find \"{findName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName)), $"Find \"{relatedFindName}\"");
                yield return new DropdownOption((Action)(() => OnFindExact(relatedFindName2)), $"Find \"{relatedFindName2}\"");
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
    }
}
