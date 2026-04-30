using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
#if !SUBGRAPH_RENAME
using SubgraphUnit = Unity.VisualScripting.SuperUnit;
#endif

namespace Unity.VisualScripting.UVSFinder
{
    internal readonly struct UVSContextSearchAction
    {
        public UVSContextSearchAction(string keyword, string label, bool exact)
        {
            Keyword = keyword;
            Label = label;
            Exact = exact;
        }

        public string Keyword { get; }
        public string Label { get; }
        public bool Exact { get; }
    }

    internal static class UVSGraphElementWidgetExt
    {
        private static readonly Dictionary<Type, Type> widgetOverrides = new Dictionary<Type, Type>
        {
            { typeof(IUnit), typeof(UVSContextSearchUnitWidget<>) },
            { typeof(IEventUnit), typeof(UVSEventContextSearchUnitWidget<>) },
            { typeof(GraphGroup), typeof(UVSGraphGroupWidget) },
            { typeof(SubgraphUnit), typeof(UVSSuperUnitWidget) },
            { typeof(StateUnit), typeof(UVSStateUnitWidget) },
            { typeof(AnyState), typeof(UVSAnyStateWidget) },
            { typeof(FlowState), typeof(UVSFlowStateWidget) },
            { typeof(SuperState), typeof(UVSSuperStateWidget) },
            { typeof(FlowStateTransition), typeof(UVSFlowStateTransitionWidget) },
            { typeof(TriggerStateTransition), typeof(UVSTriggerStateTransitionWidget) }
        };

        private static readonly FieldInfo definedDecoratorTypesField = typeof(WidgetProvider).BaseType?.GetField("definedDecoratorTypes", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo resolvedDecoratorTypesField = typeof(WidgetProvider).BaseType?.GetField("resolvedDecoratorTypes", BindingFlags.Instance | BindingFlags.NonPublic);

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            GraphWindow.activeContextChanged -= OnActiveContextChanged;
            GraphWindow.activeContextChanged += OnActiveContextChanged;

            EditorApplication.delayCall += ApplyToOpenGraphWindows;
        }

        public static IEnumerable<DropdownOption> GetDropdownOptions(IGraphElement element)
        {
            foreach (var option in GetSearchActions(element))
            {
                yield return new DropdownOption((Action)(() => OpenFinder(option.Keyword, option.Exact)), option.Label);
            }
        }

        public static void SearchInCurrentGraph()
        {
            UVSFinder.ShowUVSFinder();
            UVSFinder.GetUVSFinder().PerformSearchInCurrent("");
        }

        private static void OnActiveContextChanged(IGraphContext context)
        {
            ApplyWidgetOverrides(context?.canvas);
        }

        private static void ApplyToOpenGraphWindows()
        {
            foreach (var window in Resources.FindObjectsOfTypeAll<GraphWindow>())
            {
                ApplyWidgetOverrides(window.context?.canvas);
            }
        }

        // Visual Scripting exposes widgets as decorators, but not a public registration API.
        // Overriding the provider map lets us swap in our Harmony-free widgets while keeping
        // the same node context menu entry points.
        private static void ApplyWidgetOverrides(ICanvas canvas)
        {
            if (canvas?.widgetProvider == null || definedDecoratorTypesField == null || resolvedDecoratorTypesField == null)
            {
                return;
            }

            var definedDecoratorTypes = definedDecoratorTypesField.GetValue(canvas.widgetProvider) as Dictionary<Type, Type>;
            var resolvedDecoratorTypes = resolvedDecoratorTypesField.GetValue(canvas.widgetProvider) as Dictionary<Type, Type>;

            if (definedDecoratorTypes == null || resolvedDecoratorTypes == null)
            {
                return;
            }

            foreach (var pair in widgetOverrides)
            {
                definedDecoratorTypes[pair.Key] = pair.Value;
            }

            resolvedDecoratorTypes.Clear();
            canvas.widgetProvider.FreeAll();
            canvas.Recollect();
        }

        private static IEnumerable<UVSContextSearchAction> GetSearchActions(IGraphElement element)
        {
            if (element == null)
            {
                yield break;
            }
        }

            var name = GraphElement.GetElementName(element);

            switch (element.GetType().ToString())
            {
                case "Unity.VisualScripting.CustomEvent":
                case "Bolt.CustomEvent":
                    {
                        var curr = (CustomEvent)element;
                        var relatedFindName = $"{curr.defaultValues["name"]} [TriggerCustomEvent]";
                        yield return Exact(name, true);
                        yield return Exact(relatedFindName);
                        break;
                    }
                case "Unity.VisualScripting.TriggerCustomEvent":
                case "Bolt.TriggerCustomEvent":
                    {
                        var curr = (TriggerCustomEvent)element;
                        var relatedFindName = $"{curr.defaultValues["name"]} [CustomEvent]";
                        yield return Exact(name, true);
                        yield return Exact(relatedFindName);
                        break;
                    }
                case "Unity.VisualScripting.GetVariable":
                case "Bolt.GetVariable":
                    {
                        var curr = (GetVariable)element;
                        var relatedFindName = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                        yield return Exact(name, true);
                        yield return Exact(relatedFindName);
                        yield return Exact(relatedFindName2);
                        break;
                    }
                case "Unity.VisualScripting.SetVariable":
                case "Bolt.SetVariable":
                    {
                        var curr = (SetVariable)element;
                        var relatedFindName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Has Variable: {curr.kind}]";
                        yield return Exact(relatedFindName);
                        yield return Exact(name, true);
                        yield return Exact(relatedFindName2);
                        break;
                    }
                case "Unity.VisualScripting.IsVariableDefined":
                case "Bolt.IsVariableDefined":
                    {
                        var curr = (IsVariableDefined)element;
                        var relatedFindName = $"{curr.defaultValues["name"]} [Get Variable: {curr.kind}]";
                        var relatedFindName2 = $"{curr.defaultValues["name"]} [Set Variable: {curr.kind}]";
                        yield return Exact(relatedFindName);
                        yield return Exact(relatedFindName2);
                        yield return Exact(name, true);
                        break;
                    }
                case "Unity.VisualScripting.GetMember":
                    {
                        var curr = (GetMember)element;
                        var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Set {curr.member.name}";
                        yield return Exact(name, true);
                        yield return Exact(relatedFindName);
                        break;
                    }
                case "Unity.VisualScripting.SetMember":
                    {
                        var curr = (SetMember)element;
                        var relatedFindName = $"{curr.member.targetTypeName.Split('.').Last()} Get {curr.member.name}";
                        yield return Exact(relatedFindName);
                        yield return Exact(name, true);
                        break;
                    }
                default:
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            yield return Find(name);
                        }

                        break;
                    }
            }
        }

        private static UVSContextSearchAction Find(string keyword)
        {
            return new UVSContextSearchAction(keyword, $"Find \"{keyword}\"", false);
        }

        private static UVSContextSearchAction Exact(string keyword, bool emphasize = false)
        {
            var suffix = emphasize ? " ~" : string.Empty;
            return new UVSContextSearchAction(keyword, $"Find \"{keyword}\"{suffix}", true);
        }

        private static void OpenFinder(string keyword, bool exact)
        {
            var uvsFinder = UVSFinder.GetUVSFinder();
            uvsFinder.OnSearchAction(keyword, exact);
        }
    }

    public class UVSContextSearchUnitWidget<TUnit> : UnitWidget<TUnit>
        where TUnit : class, IUnit
    {
        public UVSContextSearchUnitWidget(FlowCanvas canvas, TUnit unit) : base(canvas, unit)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(unit))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }
    }

    public class UVSEventContextSearchUnitWidget<TEvent> : UVSContextSearchUnitWidget<TEvent>
        where TEvent : class, IEventUnit
    {
        public UVSEventContextSearchUnitWidget(FlowCanvas canvas, TEvent unit) : base(canvas, unit)
        {
        }

        protected override NodeColorMix baseColor => NodeColor.Green;
    }

    public sealed class UVSGraphGroupWidget : GraphGroupWidget
    {
        public UVSGraphGroupWidget(ICanvas canvas, GraphGroup group) : base(canvas, group)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(element))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }
    }

    public class UVSSuperUnitWidget : SuperUnitWidget
    {
        public UVSSuperUnitWidget(FlowCanvas canvas, SubgraphUnit unit) : base(canvas, unit)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(unit))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }
    }

    public class UVSStateUnitWidget : StateUnitWidget
    {
        public UVSStateUnitWidget(FlowCanvas canvas, StateUnit unit) : base(canvas, unit)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(unit))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }
    }

    public class UVSAnyStateWidget : AnyStateWidget
    {
        public UVSAnyStateWidget(StateCanvas canvas, AnyState state) : base(canvas, state)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(state))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }
    }

    public sealed class UVSTriggerStateTransitionWidget : UVSContextSearchUnitWidget<TriggerStateTransition>
    {
        public UVSTriggerStateTransitionWidget(FlowCanvas canvas, TriggerStateTransition unit) : base(canvas, unit)
        {
        }

        protected override NodeColorMix baseColor => NodeColorMix.TealReadable;
    }

    public sealed class UVSSuperStateWidget : NesterStateWidget<SuperState>, IDragAndDropHandler
    {
        public UVSSuperStateWidget(StateCanvas canvas, SuperState state) : base(canvas, state)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(state))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }

        public DragAndDropVisualMode dragAndDropVisualMode => DragAndDropVisualMode.Generic;

        public bool AcceptsDragAndDrop()
        {
            return DragAndDropUtility.Is<StateGraphAsset>();
        }

        public void PerformDragAndDrop()
        {
            UndoUtility.RecordEditedObject("Drag & Drop Macro");
            state.nest.source = GraphSource.Macro;
            state.nest.macro = DragAndDropUtility.Get<StateGraphAsset>();
            state.nest.embed = null;
            GUI.changed = true;
        }

        public void UpdateDragAndDrop()
        {
        }

        public void DrawDragAndDropPreview()
        {
            GraphGUI.DrawDragAndDropPreviewLabel(new Vector2(edgePosition.x, outerPosition.yMax), "Replace with: " + DragAndDropUtility.Get<StateGraphAsset>().name, typeof(StateGraphAsset).Icon());
        }

        public void ExitDragAndDrop()
        {
        }
    }

    public sealed class UVSFlowStateTransitionWidget : NesterStateTransitionWidget<FlowStateTransition>, IDragAndDropHandler
    {
        public UVSFlowStateTransitionWidget(StateCanvas canvas, FlowStateTransition transition) : base(canvas, transition)
        {
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(transition))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }

        public DragAndDropVisualMode dragAndDropVisualMode => DragAndDropVisualMode.Generic;

        public bool AcceptsDragAndDrop()
        {
            return DragAndDropUtility.Is<ScriptGraphAsset>();
        }

        public void PerformDragAndDrop()
        {
            UndoUtility.RecordEditedObject("Drag & Drop Macro");
            transition.nest.source = GraphSource.Macro;
            transition.nest.macro = DragAndDropUtility.Get<ScriptGraphAsset>();
            transition.nest.embed = null;
            GUI.changed = true;
        }

        public void UpdateDragAndDrop()
        {
        }

        public void DrawDragAndDropPreview()
        {
            GraphGUI.DrawDragAndDropPreviewLabel(new Vector2(edgePosition.x, outerPosition.yMax), "Replace with: " + DragAndDropUtility.Get<ScriptGraphAsset>().name, typeof(ScriptGraphAsset).Icon());
        }

        public void ExitDragAndDrop()
        {
        }
    }

    public sealed class UVSFlowStateWidget : NesterStateWidget<FlowState>, IDragAndDropHandler
    {
        public UVSFlowStateWidget(StateCanvas canvas, FlowState state) : base(canvas, state)
        {
            state.nest.beforeGraphChange += BeforeGraphChange;
            state.nest.afterGraphChange += AfterGraphChange;

            if (state.nest.graph != null)
            {
                state.nest.graph.elements.CollectionChanged += CacheEventLinesOnUnityThread;
            }
        }

        public override void Dispose()
        {
            base.Dispose();

            state.nest.beforeGraphChange -= BeforeGraphChange;
            state.nest.afterGraphChange -= AfterGraphChange;
        }

        private void BeforeGraphChange()
        {
            if (state.nest.graph != null)
            {
                state.nest.graph.elements.CollectionChanged -= CacheEventLinesOnUnityThread;
            }
        }

        private void AfterGraphChange()
        {
            CacheEventLinesOnUnityThread();

            if (state.nest.graph != null)
            {
                state.nest.graph.elements.CollectionChanged += CacheEventLinesOnUnityThread;
            }
        }

        private List<EventLine> eventLines { get; } = new List<EventLine>();

        private void CacheEventLinesOnUnityThread()
        {
            UnityAPI.Async(CacheEventLines);
        }

        private void CacheEventLines()
        {
            eventLines.Clear();

            if (state.nest.graph != null)
            {
                eventLines.AddRange(state.nest.graph.units
                    .OfType<IEventUnit>()
                    .Select(e => e.GetType())
                    .Distinct()
                    .Select(eventType => new EventLine(eventType))
                    .OrderBy(eventLine => eventLine.content.text));
            }

            Reposition();
        }

        protected override void CacheItemFirstTime()
        {
            base.CacheItemFirstTime();

            CacheEventLines();
        }

        public Dictionary<EventLine, Rect> eventLinesPositions { get; } = new Dictionary<EventLine, Rect>();

        public override void CachePosition()
        {
            base.CachePosition();

            eventLinesPositions.Clear();

            var y = contentInnerPosition.y;

            foreach (var eventLine in eventLines)
            {
                var eventLinePosition = new Rect
                (
                    contentInnerPosition.x,
                    y,
                    contentInnerPosition.width,
                    eventLine.GetHeight(contentInnerPosition.width)
                );

                eventLinesPositions.Add(eventLine, eventLinePosition);

                y += eventLinePosition.height;
            }
        }

        protected override float GetContentHeight(float width)
        {
            var eventLinesHeight = 0f;

            foreach (var eventLine in eventLines)
            {
                eventLinesHeight += eventLine.GetHeight(width);
            }

            return eventLinesHeight;
        }

        protected override bool showContent => eventLines.Count > 0;

        protected override void DrawContent()
        {
            foreach (var eventLine in eventLines)
            {
                eventLine.Draw(eventLinesPositions[eventLine]);
            }
        }

        protected override IEnumerable<DropdownOption> contextOptions
        {
            get
            {
                foreach (var option in UVSGraphElementWidgetExt.GetDropdownOptions(state))
                {
                    yield return option;
                }

                foreach (var baseOption in base.contextOptions)
                {
                    yield return baseOption;
                }
            }
        }

        public DragAndDropVisualMode dragAndDropVisualMode => DragAndDropVisualMode.Generic;

        public bool AcceptsDragAndDrop()
        {
            return DragAndDropUtility.Is<ScriptGraphAsset>();
        }

        public void PerformDragAndDrop()
        {
            UndoUtility.RecordEditedObject("Drag & Drop Macro");
            state.nest.source = GraphSource.Macro;
            state.nest.macro = DragAndDropUtility.Get<ScriptGraphAsset>();
            state.nest.embed = null;
            GUI.changed = true;
        }

        public void UpdateDragAndDrop()
        {
        }

        public void DrawDragAndDropPreview()
        {
            GraphGUI.DrawDragAndDropPreviewLabel(new Vector2(edgePosition.x, outerPosition.yMax), "Replace with: " + DragAndDropUtility.Get<ScriptGraphAsset>().name, typeof(ScriptGraphAsset).Icon());
        }

        public void ExitDragAndDrop()
        {
        }

        public new static class Styles
        {
            static Styles()
            {
                eventLine = new GUIStyle(EditorStyles.label);
                eventLine.wordWrap = true;
                eventLine.imagePosition = ImagePosition.TextOnly;
                eventLine.padding = new RectOffset(0, 0, 3, 3);
            }

            public static readonly GUIStyle eventLine;
            public static readonly float spaceAroundLineIcon = 5;
        }

        public class EventLine
        {
            public EventLine(Type eventType)
            {
                content = new GUIContent(BoltFlowNameUtility.UnitTitle(eventType, false, true), eventType.Icon()?[IconSize.Small]);
            }

            public GUIContent content { get; }

            public float GetHeight(float width)
            {
                var labelWidth = width - Styles.spaceAroundLineIcon - IconSize.Small - Styles.spaceAroundLineIcon;

                return Styles.eventLine.CalcHeight(content, labelWidth);
            }

            public void Draw(Rect position)
            {
                var iconPosition = new Rect
                (
                    position.x + Styles.spaceAroundLineIcon,
                    position.y + Styles.eventLine.padding.top - 1,
                    IconSize.Small,
                    IconSize.Small
                );

                var labelPosition = new Rect
                (
                    iconPosition.xMax + Styles.spaceAroundLineIcon,
                    position.y,
                    position.width - Styles.spaceAroundLineIcon - iconPosition.width - Styles.spaceAroundLineIcon,
                    position.height
                );

                GUI.DrawTexture(iconPosition, content.image);
                GUI.Label(labelPosition, content, Styles.eventLine);
            }
        }
    }
}
