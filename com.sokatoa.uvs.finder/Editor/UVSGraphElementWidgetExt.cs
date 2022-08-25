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
            var harmony = new Harmony("com.example.patch");
            harmony.PatchAll();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GraphElementWidget<ICanvas, IGraphElement>), "contextOptions", MethodType.Getter)]
        static IEnumerable<DropdownOption> Postfix1(IEnumerable<DropdownOption> __result, GraphElementWidget<ICanvas, IGraphElement> __instance){ 
            // UnitWidget > NodeWidget > GraphElementWidget
            // SUperUnitWidget > NesterUnitWidget > UnitWidget
            var canvasProp = __instance.GetType().GetProperty("canvas", BindingFlags.NonPublic | BindingFlags.Instance);
            ICanvas canvas = (ICanvas)canvasProp.GetValue(__instance);

            var elementProp = __instance.GetType().GetProperty("element", BindingFlags.NonPublic | BindingFlags.Instance);
            
            IGraphElement element = (IGraphElement)elementProp?.GetValue(__instance);

            // --------------------
            // adding my own options
            // TODO: generalize this...
            if ((__instance as IUnitWidget).unit is CustomEvent)
            {
                yield return new DropdownOption((Action)(() => OnFind($"{((__instance as IUnitWidget).unit as CustomEvent).defaultValues["name"]} [CustomEvent]")), $"Find \"{((__instance as IUnitWidget).unit as CustomEvent).defaultValues["name"]} [CustomEvent]\"");
                yield return new DropdownOption((Action)(() => OnFind($"{((__instance as IUnitWidget).unit as CustomEvent).defaultValues["name"]} [TriggerCustomEvent]")), $"Find \"{((__instance as IUnitWidget).unit as CustomEvent).defaultValues["name"]} [TriggerCustomEvent]\"");
            }
            if ((__instance as IUnitWidget).unit is TriggerCustomEvent)
            {
                yield return new DropdownOption((Action)(() => OnFind($"{((__instance as IUnitWidget).unit as TriggerCustomEvent).defaultValues["name"]} [TriggerCustomEvent]")), $"Find \"{((__instance as IUnitWidget).unit as TriggerCustomEvent).defaultValues["name"]} [TriggerCustomEvent]\"");
                yield return new DropdownOption((Action)(() => OnFind($"{((__instance as IUnitWidget).unit as TriggerCustomEvent).defaultValues["name"]} [CustomEvent]")), $"Find \"{((__instance as IUnitWidget).unit as TriggerCustomEvent).defaultValues["name"]} [CustomEvent]\"");
            }

            // --------------------
            // This is recreating the list returned by GraphElementWidget's get contextOptions
            var suffix = canvas.selection.Count > 1 ? " Selection" : "";

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

        private static void OnFind(string keyword)
        {
            Debug.Log("on find!!");
            var uvsfinder = UVSFinder.GetUVSFinder();
            uvsfinder.PerformSearchInCurrent(keyword);
        }
    }
}
