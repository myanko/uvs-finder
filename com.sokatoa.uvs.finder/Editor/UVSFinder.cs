using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using UnityObject = UnityEngine.Object;
using UnityEditor.SceneManagement;
#if !SUBGRAPH_RENAME
using SubgraphUnit = Unity.VisualScripting.SuperUnit;
using ScriptMachine = Unity.VisualScripting.FlowMachine;
#endif

namespace Unity.VisualScripting.UVSFinder
{
    [Serializable]
    public class UVSFinder : EditorWindow
    {
        internal static UVSFinderPreferences prefs => UVSFinderSettingsProvider.Preferences;

        [MenuItem("Tools/VisualScripting/Node Finder All Graphs &F")]
        public static void ShowUVSFinder()
        {
            EditorWindow wnd = GetWindow<UVSFinder>();
            wnd.titleContent = new GUIContent("Node Finder");
        }

        public static UVSFinder GetUVSFinder()
        {
            return GetWindow<UVSFinder>("UVSFinder");
        }

        public int searchCount = 0;
        public List<string> nodeList = new List<string>();
        public ToolbarPopupSearchField searchField;
        private enum UVSFinderTabs {
            current,
            all,
            hierarchy
        }
        private Dictionary<UVSFinderTabs, List<ResultItem>> searchItems = new Dictionary<UVSFinderTabs, List<ResultItem>>(){
            { UVSFinderTabs.current, new List<ResultItem>() },
            { UVSFinderTabs.all, new List<ResultItem>() },
            { UVSFinderTabs.hierarchy, new List<ResultItem>() }
        };
        public ListView resultListview;
        private Foldout replaceFoldout;
        private TextField replaceField;
        private DropdownField replaceMode;
        private Button useSelectedNodeButton;
        private Button replaceSelectedButton;
        private Button replaceAllButton;
        private IMGUIContainer graphPreview;
        private UVSReplaceMode selectedReplaceMode = UVSReplaceMode.Values;
        private IUnit replacementNodeTemplate;
        private string replacementNodeTemplateName;
        private bool lastSearchExact;
        private bool variableRenameSearchActive;
        private string variableRenameName;
        private VariableKind variableRenameKind;
        private bool eventRenameSearchActive;
        private UVSEventRenameInfo eventRenameInfo;
        private const string ReplaceModeValuesLabel = "Values";
        private const string ReplaceModeNodeLabel = "Node";

        private Button tabCurrentGraph;
        private Toggle enableCurrentGraphSearch;
        private Button tabAllGraphs;
        private Toggle enableAllGraphsSearch;
        private Button tabHierarchyGraphButton;
        private Toggle enableHierarchySearch;
        private UVSFinderTabs selectedTab;

        public void CreateGUI()
        {
            var path = new UVSFinderPaths().findRootPackagePath();
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy.
            /*VisualElement label = new Label("Hello World! From C#");
            root.Add(label);*/

            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path + "/UI/UVSFinder.uxml");
            VisualElement UVSFinderFromUXML = visualTree.CloneTree();
            root.Add(UVSFinderFromUXML);

            //Register Keys Events
            root.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);

            // Get a reference to the Button from UXML and assign it its action.
            searchField = root.Q<ToolbarPopupSearchField>("search-field");
            var nodePathLabel = root.Q<Label>("node-path-label");
            var searchOptions = root.Q<Button>("searchOptions");
            replaceFoldout = root.Q<Foldout>("replace-foldout");
            replaceField = root.Q<TextField>("replace-field");
            replaceMode = root.Q<DropdownField>("replace-mode");
            useSelectedNodeButton = root.Q<Button>("use-selected-node");
            replaceSelectedButton = root.Q<Button>("replace-selected");
            replaceAllButton = root.Q<Button>("replace-all");
            SetupReplaceControls();
            //listItem
            var listItem = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path + "/UI/listItem.uxml");

            // Tabs
            tabCurrentGraph = root.Q<Button>("currentGraphButton");
            tabCurrentGraph.clicked += OnCurrentGraphClick;
            enableCurrentGraphSearch = root.Q<Toggle>("toggleEnableCurrentGraphSearch");
            enableCurrentGraphSearch.value = prefs.enableCurrentGraphSearch;
            enableCurrentGraphSearch.RegisterValueChangedCallback(OnEnableCurrentGraphSearchValueChanged);
            tabAllGraphs = root.Q<Button>("allGraphsButton");
            tabAllGraphs.clicked += OnAllGraphsClick;
            enableAllGraphsSearch = root.Q<Toggle>("toggleEnableAllGraphsSearch");
            enableAllGraphsSearch.value = prefs.enableAllGraphsSearch;
            enableAllGraphsSearch.RegisterValueChangedCallback(OnEnableAllGraphsSearchValueChanged);
            tabHierarchyGraphButton = root.Q<Button>("hierarchyGraphButton");
            tabHierarchyGraphButton.clicked += OnHierarchyGraphClick;
            enableHierarchySearch = root.Q<Toggle>("toggleEnableHierarchySearch");
            enableHierarchySearch.value = prefs.enableHierarchySearch;
            enableHierarchySearch.RegisterValueChangedCallback(OnEnableHierarchySearchValueChanged);
            selectedTab = UVSFinderTabs.all;

            // The "makeItem" function will be called as needed
            // when the ListView needs more items to render
            Func<VisualElement> makeItem = () => listItem.Instantiate();

            // As the user scrolls through the list, the ListView object
            // will recycle elements created by the "makeItem"
            // and invoke the "bindItem" callback to associate
            // the element with the matching data item (specified as an index in the list)
            // consider it as dirty when modifying it
            Action<VisualElement, int> bindItem = (e, i) =>
            {
                //TODO: Case Current/All/FindReplace/Other
                if ((selectedTab == UVSFinderTabs.current && prefs.showTypeIconCurrent) ||
                    (selectedTab == UVSFinderTabs.all && prefs.showTypeIconAll)||
                    (selectedTab == UVSFinderTabs.hierarchy && prefs.showTypeIconHierarchy))
                {
                    var icon = GetIcon(searchItems[selectedTab][i]);
                    e.Q<Label>("Icon").style.backgroundImage = new StyleBackground(icon);
                }
                var resultItem = searchItems[selectedTab][i];
                var resultRow = e.Q<VisualElement>("ResultRow") ?? e;
                resultRow.EnableInClassList("result-row-selected", IsResultSelected(resultItem));

                var description = e.Q<VisualElement>("Description");
                description.hierarchy.Clear();

                TextHighlighter.HighlightTextBasedOnQuery(description, resultItem.itemName, searchField.value);
                description.AddToClassList("highlightdone");
                OverwriteHighlightColor(e);

                var filePath = e.Q<Label>("FilePath");
                filePath.text = resultItem.assetPath;
                if (!String.IsNullOrEmpty(resultItem.stateName))
                {
                    filePath.text += $" > {resultItem.stateName}";
                }
                if (resultItem.gameObject != null)
                {
                    filePath.text += $" ({resultItem.gameObject.name})";
                }

                var replacementPreview = e.Q<Label>("ReplacementPreview");
                if (replacementPreview != null)
                {
                    if (IsReplaceEnabled() && resultItem.replacePreview != null)
                    {
                        replacementPreview.text = resultItem.replacePreview.Summary;
                        replacementPreview.style.display = DisplayStyle.Flex;
                        replacementPreview.EnableInClassList("replace-preview-disabled", !resultItem.replacePreview.canReplace);
                    }
                    else
                    {
                        replacementPreview.text = "";
                        replacementPreview.style.display = DisplayStyle.None;
                        replacementPreview.EnableInClassList("replace-preview-disabled", false);
                    }
                }
            };

            resultListview = root.Q<ListView>("results-list");
            resultListview.makeItem = makeItem;
            resultListview.bindItem = bindItem;
            resultListview.itemsSource = searchItems[selectedTab];
            resultListview.selectionType = SelectionType.Multiple;

            // Callback invoked when the user double clicks an item
            resultListview.onItemsChosen += OnItemsChosen;

            // Callback invoked when the user changes the selection inside the ListView
            resultListview.onSelectionChange += OnItemsChosen;
            resultListview.onSelectionChange += _ =>
            {
                RefreshResultSelectionStyles();
                graphPreview?.MarkDirtyRepaint();
            };
            var graphPreviewHost = root.Q<VisualElement>("graph-preview-host");
            if (graphPreviewHost != null)
            {
                graphPreview = new IMGUIContainer(DrawGraphPreview);
                graphPreview.style.flexGrow = 1;
                graphPreview.style.flexShrink = 0;
                graphPreview.style.width = Length.Percent(100);
                graphPreview.style.height = 116;
                graphPreview.style.minHeight = 116;
                graphPreviewHost.Add(graphPreview);
            }
            if (searchOptions != null)
            {
                searchOptions.clickable.clicked += () =>
                {
                    SettingsService.OpenUserPreferences("Preferences/Visual Scripting/UVS Finder");
                };
            }
            GetWindow<UVSFinder>();
            searchField.Focus();
        }

        private void OverwriteHighlightColor(VisualElement e)
        {
            if(prefs.textHighLightColor != new Color(255, 128, 0))
            {
                var highlighted = e.Q<Label>(className: "Highlighted");
                if (highlighted != null)
                {
                    highlighted.style.color = prefs.textHighLightColor;
                }
            }
        }

        private void SetupReplaceControls()
        {
            if (replaceMode != null)
            {
                replaceMode.choices = new List<string> { ReplaceModeValuesLabel, ReplaceModeNodeLabel };
                replaceMode.SetValueWithoutNotify(ReplaceModeValuesLabel);
                replaceMode.RegisterValueChangedCallback(OnReplaceModeChanged);
            }

            replaceFoldout?.RegisterValueChangedCallback(_ =>
            {
                UpdateReplaceControls();
                RefreshReplacePreview();
            });

            replaceField?.RegisterValueChangedCallback(_ =>
            {
                if (IsReplaceEnabled())
                {
                    RefreshReplacePreview();
                }
            });

            useSelectedNodeButton?.RegisterCallback<ClickEvent>(_ => CaptureSelectedNodeTemplate());
            replaceSelectedButton?.RegisterCallback<ClickEvent>(_ => ReplaceSelectedItems());
            replaceAllButton?.RegisterCallback<ClickEvent>(_ => ReplaceAllInSelectedTab());

            UpdateReplaceControls();
        }

        private void OnReplaceModeChanged(ChangeEvent<string> evt)
        {
            selectedReplaceMode = evt.newValue == ReplaceModeNodeLabel ? UVSReplaceMode.Node : UVSReplaceMode.Values;
            UpdateReplaceControls();
            RefreshReplacePreview();
        }

        private void UpdateReplaceControls()
        {
            var enabled = IsReplaceEnabled();
            var valuesMode = selectedReplaceMode == UVSReplaceMode.Values;
            var nodeMode = selectedReplaceMode == UVSReplaceMode.Node;

            if (replaceField != null)
            {
                replaceField.style.display = valuesMode ? DisplayStyle.Flex : DisplayStyle.None;
                replaceField.SetEnabled(enabled && valuesMode);
            }

            replaceMode?.SetEnabled(enabled);
            replaceSelectedButton?.SetEnabled(enabled);
            replaceAllButton?.SetEnabled(enabled);

            if (useSelectedNodeButton != null)
            {
                useSelectedNodeButton.style.display = nodeMode ? DisplayStyle.Flex : DisplayStyle.None;
                useSelectedNodeButton.SetEnabled(enabled && nodeMode);

                if (string.IsNullOrEmpty(replacementNodeTemplateName))
                {
                    useSelectedNodeButton.text = "Pick";
                    useSelectedNodeButton.tooltip = "Use the selected node in the current graph as the replacement template.";
                }
                else
                {
                    useSelectedNodeButton.text = "Template";
                    useSelectedNodeButton.tooltip = $"Replacement node: {replacementNodeTemplateName}";
                }
            }
        }

        public void StartReplaceFromSelection()
        {
            if (replaceFoldout != null)
            {
                replaceFoldout.value = true;
            }

            CaptureSelectedNodeTemplate();
        }

        public void StartVariableRename(string variableName, VariableKind variableKind)
        {
            if (string.IsNullOrEmpty(variableName))
            {
                EditorUtility.DisplayDialog("Rename Variable", "The selected variable does not have a name to rename.", "OK");
                return;
            }

            variableRenameSearchActive = true;
            variableRenameName = variableName;
            variableRenameKind = variableKind;
            eventRenameSearchActive = false;
            eventRenameInfo = default;
            lastSearchExact = true;
            selectedReplaceMode = UVSReplaceMode.Values;
            replacementNodeTemplate = null;
            replacementNodeTemplateName = null;

            searchField.value = variableName;
            replaceMode?.SetValueWithoutNotify(ReplaceModeValuesLabel);
            replaceField?.SetValueWithoutNotify(variableName);

            if (replaceFoldout != null)
            {
                replaceFoldout.value = true;
            }

            Focus();
            ResetResultItems();
            PerformVariableRenameSearch();
            UpdateReplaceControls();
            RefreshReplacePreview();
            setWindowTitle();
            setTabsResults();
            DisplayResultsItems();
            replaceField?.Focus();
        }

        internal void StartEventRename(UVSEventRenameInfo eventInfo)
        {
            if (!eventInfo.IsValid)
            {
                EditorUtility.DisplayDialog("Rename Event", "The selected event does not have a name to rename.", "OK");
                return;
            }

            eventRenameSearchActive = true;
            this.eventRenameInfo = eventInfo;
            variableRenameSearchActive = false;
            variableRenameName = null;
            lastSearchExact = true;
            selectedReplaceMode = UVSReplaceMode.Values;
            replacementNodeTemplate = null;
            replacementNodeTemplateName = null;

            searchField.value = eventInfo.ValueText;
            replaceMode?.SetValueWithoutNotify(ReplaceModeValuesLabel);
            replaceField?.SetValueWithoutNotify(eventInfo.ValueText);

            if (replaceFoldout != null)
            {
                replaceFoldout.value = true;
            }

            Focus();
            ResetResultItems();
            PerformEventRenameSearch();
            UpdateReplaceControls();
            RefreshReplacePreview();
            setWindowTitle();
            setTabsResults();
            DisplayResultsItems();
            replaceField?.Focus();
        }

        public void StartEventRename(string eventName)
        {
            StartEventRename(new UVSEventRenameInfo(eventName, "name", "Unity.VisualScripting.CustomEvent"));
        }

        private void CaptureSelectedNodeTemplate()
        {
            var selectedUnit = GraphWindow.active?.context?.selection?.OfType<IUnit>().FirstOrDefault();
            if (selectedUnit == null)
            {
                EditorUtility.DisplayDialog("Find And Replace", "Select a flow node in the graph first, then use it as the replacement template.", "OK");
                return;
            }

            replacementNodeTemplate = selectedUnit.CloneViaSerialization();
            replacementNodeTemplateName = GraphElement.GetElementName((IGraphElement)replacementNodeTemplate);
            selectedReplaceMode = UVSReplaceMode.Node;
            replaceMode?.SetValueWithoutNotify(ReplaceModeNodeLabel);

            if (replaceFoldout != null)
            {
                replaceFoldout.value = true;
            }

            UpdateReplaceControls();
            RefreshReplacePreview();
        }

        private void RefreshReplacePreview()
        {
            if (!IsReplaceEnabled())
            {
                ClearReplacePreviews();
                DisplayResultsItems();
                graphPreview?.MarkDirtyRepaint();
                return;
            }

            foreach (var resultItem in searchItems.Values.SelectMany(items => items))
            {
                resultItem.replacePreview = BuildReplacePreview(resultItem);
            }

            DisplayResultsItems();
            graphPreview?.MarkDirtyRepaint();
        }

        private UVSReplacePreview BuildReplacePreview(ResultItem resultItem)
        {
            if (selectedReplaceMode == UVSReplaceMode.Node)
            {
                return UVSReplaceProvider.PreviewNodeReplacement(resultItem, replacementNodeTemplate);
            }

            return UVSReplaceProvider.PreviewValueReplacement(resultItem, GetReplaceFindValue(), replaceField?.value ?? string.Empty, lastSearchExact);
        }

        private void ReplaceSelectedItems()
        {
            if (!IsReplaceEnabled())
            {
                return;
            }

            var selectedItems = GetSelectedResultItems().ToList();
            if (selectedItems.Count == 0)
            {
                EditorUtility.DisplayDialog("Find And Replace", "Select one or more results to replace.", "OK");
                return;
            }

            foreach (var resultItem in selectedItems)
            {
                resultItem.replacePreview = BuildReplacePreview(resultItem);
            }

            var replaceableItems = selectedItems
                .Where(item => item.replacePreview?.canReplace == true)
                .ToList();

            if (replaceableItems.Count == 0)
            {
                EditorUtility.DisplayDialog("Find And Replace", "No replaceable selected results.", "OK");
                return;
            }

            if (replaceableItems.Count > 1 &&
                !EditorUtility.DisplayDialog("Find And Replace", $"Replace {replaceableItems.Count} selected result(s)?", "Replace", "Cancel"))
            {
                return;
            }

            PrepareForReplaceMutation(replaceableItems);

            var replacedCount = 0;
            foreach (var resultItem in replaceableItems)
            {
                replacedCount += ReplaceResultItem(resultItem);
            }

            if (replacedCount > 0)
            {
                RefreshSearchAfterReplace();
            }
        }

        private void ReplaceAllInSelectedTab()
        {
            if (!IsReplaceEnabled())
            {
                return;
            }

            RefreshReplacePreview();
            var replaceableItems = searchItems[selectedTab]
                .Where(item => item.replacePreview?.canReplace == true)
                .ToList();

            if (replaceableItems.Count == 0)
            {
                EditorUtility.DisplayDialog("Find And Replace", "No replaceable results in the selected tab.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Find And Replace", $"Replace {replaceableItems.Count} result(s) in {selectedTab}?", "Replace", "Cancel"))
            {
                return;
            }

            PrepareForReplaceMutation(replaceableItems);

            var replacedCount = 0;
            foreach (var resultItem in replaceableItems)
            {
                replacedCount += ReplaceResultItem(resultItem);
            }

            if (replacedCount > 0)
            {
                RefreshSearchAfterReplace();
            }
        }

        private void RefreshSearchAfterReplace()
        {
            ClearResultSelection();

            if (variableRenameSearchActive)
            {
                ResetResultItems();
                PerformVariableRenameSearch();
                RefreshReplacePreview();
                setWindowTitle();
                setTabsResults();
                DisplayResultsItems();
                FinalizeReplaceRefresh();
                return;
            }

            if (eventRenameSearchActive)
            {
                ResetResultItems();
                PerformEventRenameSearch();
                RefreshReplacePreview();
                setWindowTitle();
                setTabsResults();
                DisplayResultsItems();
                FinalizeReplaceRefresh();
                return;
            }

            OnSearchAction(searchField.value, lastSearchExact);
            FinalizeReplaceRefresh();
        }

        private void PrepareForReplaceMutation(IReadOnlyCollection<ResultItem> replaceableItems)
        {
            ClearResultSelection();
            ClearActiveGraphSelectionForReplace(replaceableItems);
            graphPreview?.MarkDirtyRepaint();
        }

        private void FinalizeReplaceRefresh()
        {
            ClearResultSelection();
            RecollectActiveGraphCanvas();
            graphPreview?.MarkDirtyRepaint();
        }

        private void ClearResultSelection()
        {
            if (resultListview == null)
            {
                return;
            }

            resultListview.ClearSelection();
        }

        private void ClearActiveGraphSelectionForReplace(IEnumerable<ResultItem> replaceableItems)
        {
            var graphWindow = GraphWindow.active;
            var selection = graphWindow?.context?.selection;
            if (selection == null)
            {
                return;
            }

            var shouldClearSelection = selectedReplaceMode == UVSReplaceMode.Node;
            if (!shouldClearSelection && replaceableItems != null)
            {
                foreach (var resultItem in replaceableItems)
                {
                    if (resultItem?.graphElement != null && selection.Contains(resultItem.graphElement))
                    {
                        shouldClearSelection = true;
                        break;
                    }
                }
            }

            if (!shouldClearSelection)
            {
                return;
            }

            selection.Clear();
            RecollectActiveGraphCanvas();
        }

        private void RecollectActiveGraphCanvas()
        {
            var graphWindow = GraphWindow.active;
            try
            {
                graphWindow?.context?.canvas?.Recollect();
                graphWindow?.Repaint();
            }
            catch
            {
                // Graph windows can become temporarily invalid while switching references.
            }
        }

        private int ReplaceResultItem(ResultItem resultItem)
        {
            if (selectedReplaceMode == UVSReplaceMode.Node)
            {
                return UVSReplaceProvider.ReplaceNode(resultItem, replacementNodeTemplate);
            }

            return UVSReplaceProvider.ReplaceValues(resultItem, GetReplaceFindValue(), replaceField?.value ?? string.Empty, lastSearchExact);
        }

        private string GetReplaceFindValue()
        {
            if (variableRenameSearchActive)
            {
                return variableRenameName;
            }

            return eventRenameSearchActive ? eventRenameInfo.ValueText : searchField.value;
        }

        private bool IsReplaceEnabled()
        {
            return replaceFoldout?.value == true;
        }

        private List<ResultItem> GetSelectedResultItems()
        {
            var selectedItems = new List<ResultItem>();
            if (resultListview == null)
            {
                return selectedItems;
            }

            foreach (var resultItem in resultListview.selectedItems.OfType<ResultItem>())
            {
                if (!selectedItems.Contains(resultItem))
                {
                    selectedItems.Add(resultItem);
                }
            }

            if (selectedItems.Count == 0 && resultListview.selectedItem is ResultItem singleResultItem)
            {
                selectedItems.Add(singleResultItem);
            }

            return selectedItems;
        }

        private bool IsResultSelected(ResultItem resultItem)
        {
            if (resultListview == null || resultItem == null)
            {
                return false;
            }

            foreach (var selectedItem in resultListview.selectedItems.OfType<ResultItem>())
            {
                if (ReferenceEquals(selectedItem, resultItem))
                {
                    return true;
                }
            }

            return false;
        }

        private void RefreshResultSelectionStyles()
        {
            if (resultListview == null)
            {
                return;
            }

#if UNITY_2021_2_OR_NEWER
            resultListview.Rebuild();
#else
            resultListview.Refresh();
#endif
        }

        private ResultItem GetPrimarySelectedResultItem(IEnumerable<object> items = null)
        {
            if (resultListview?.selectedItem is ResultItem selectedItem)
            {
                return selectedItem;
            }

            var itemFromArgument = items?.OfType<ResultItem>().FirstOrDefault();
            if (itemFromArgument != null)
            {
                return itemFromArgument;
            }

            return GetSelectedResultItems().FirstOrDefault();
        }

        private void ClearReplacePreviews()
        {
            foreach (var resultItem in searchItems.Values.SelectMany(items => items))
            {
                resultItem.replacePreview = null;
            }
        }

        private void DrawGraphPreview()
        {
            var resultItem = GetPrimarySelectedResultItem();
            var width = graphPreview?.resolvedStyle.width ?? 0;
            var height = graphPreview?.resolvedStyle.height ?? 0;

            if (float.IsNaN(width) || width <= 0)
            {
                width = position.width;
            }

            if (float.IsNaN(height) || height <= 0)
            {
                height = 116;
            }

            const float gap = 6f;
            const float labelHeight = 18f;
            var paneWidth = Mathf.Max(60, (width - gap) * 0.5f);
            var leftLabel = new Rect(0, 0, paneWidth, labelHeight);
            var rightLabel = new Rect(paneWidth + gap, 0, paneWidth, labelHeight);
            var leftPane = new Rect(0, labelHeight, paneWidth, Mathf.Max(40, height - labelHeight));
            var rightPane = new Rect(paneWidth + gap, labelHeight, paneWidth, Mathf.Max(40, height - labelHeight));

            GUI.Label(leftLabel, "Before", EditorStyles.miniBoldLabel);
            GUI.Label(rightLabel, "After", EditorStyles.miniBoldLabel);
            DrawGraphPreviewPane(leftPane, resultItem, false);
            DrawGraphPreviewPane(rightPane, resultItem, true);
        }

        private void DrawGraphPreviewPane(Rect rect, ResultItem resultItem, bool after)
        {
            GUI.Box(rect, GUIContent.none);

            if (Event.current.type == EventType.Repaint)
            {
                if (resultItem == null)
                {
                    DrawPreviewMessage(rect, "Select a result");
                }
                else if (after && IsReplaceEnabled())
                {
                    DrawAfterGraphPreview(rect, resultItem);
                }
                else
                {
                    DrawActiveGraphWidgetPreview(rect, resultItem, resultItem.itemName);
                }
            }
        }

        private void DrawAfterGraphPreview(Rect rect, ResultItem resultItem)
        {
            if (resultItem.replacePreview?.canReplace != true)
            {
                DrawPreviewMessage(rect, resultItem.replacePreview?.Summary ?? resultItem.itemName ?? "Preview first");
                return;
            }

            if (selectedReplaceMode == UVSReplaceMode.Node)
            {
                DrawReplacementTemplatePreview(rect);
                return;
            }

            try
            {
                UVSReplaceProvider.WithValuePreview(
                    resultItem,
                    GetReplaceFindValue(),
                    replaceField?.value ?? string.Empty,
                    lastSearchExact,
                    () => DrawActiveGraphWidgetPreview(rect, resultItem, resultItem.replacePreview.afterName));
            }
            finally
            {
                CacheActiveGraphPreview();
            }
        }

        private void DrawActiveGraphWidgetPreview(Rect rect, ResultItem resultItem, string fallbackText)
        {
            var graphContext = GraphWindow.active?.context;
            var canvas = graphContext?.canvas;
            if (!CanDrawGraphElement(canvas, resultItem?.graphElement))
            {
                DrawPreviewMessage(rect, fallbackText ?? "Open the result");
                return;
            }

            var beganEdit = false;
            try
            {
                graphContext.BeginEdit(false);
                beganEdit = true;
                PrepareGraphPreviewCanvas(canvas);
                var widget = canvas.Widget(resultItem.graphElement);
                widget.CacheItem();
                widget.CachePosition();
                DrawWidgetPreview(rect, widget);
            }
            catch
            {
                DrawPreviewMessage(rect, fallbackText ?? "Preview unavailable");
            }
            finally
            {
                if (beganEdit)
                {
                    graphContext.EndEdit();
                }
            }
        }

        private bool CanDrawGraphElement(ICanvas canvas, IGraphElement graphElement)
        {
            if (canvas?.graph == null || graphElement == null)
            {
                return false;
            }

            if (ReferenceEquals(graphElement.graph, canvas.graph))
            {
                return true;
            }

            try
            {
                return canvas.graph.elements.Any(element => ReferenceEquals(element, graphElement));
            }
            catch
            {
                return false;
            }
        }

        private void DrawReplacementTemplatePreview(Rect rect)
        {
            if (replacementNodeTemplate == null)
            {
                DrawPreviewMessage(rect, "Pick a node template");
                return;
            }

            ScriptGraphAsset previewMacro = null;
            IGraphContext previewContext = null;
            var beganEdit = false;

            try
            {
                previewMacro = CreateInstance<ScriptGraphAsset>();
                previewMacro.graph = new FlowGraph();

                var previewUnit = replacementNodeTemplate.CloneViaSerialization();
                previewUnit.guid = Guid.NewGuid();
                previewUnit.position = Vector2.zero;
                previewMacro.graph.units.Add(previewUnit);

                var previewReference = GraphReference.New(previewMacro, true);
                previewContext = previewReference.Context();
                previewContext.BeginEdit(false);
                beganEdit = true;
                PrepareGraphPreviewCanvas(previewContext.canvas);

                var widget = previewContext.canvas.Widget(previewUnit);
                widget.CacheItem();
                widget.CachePosition();
                DrawWidgetPreview(rect, widget);
            }
            catch
            {
                DrawPreviewMessage(rect, replacementNodeTemplateName ?? "Preview unavailable");
            }
            finally
            {
                try
                {
                    if (beganEdit)
                    {
                        previewContext?.EndEdit();
                    }

                    previewContext?.Dispose();
                }
                catch
                {
                    // Ignore cleanup issues in the transient preview graph.
                }

                if (previewMacro != null)
                {
                    DestroyImmediate(previewMacro);
                }
            }
        }

        private void PrepareGraphPreviewCanvas(ICanvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            canvas.window = GraphWindow.active;
            canvas.Cache();
            canvas.RegisterControls();
            canvas.BeforeFrame();
        }

        private void CacheActiveGraphPreview()
        {
            var graphContext = GraphWindow.active?.context;
            if (graphContext?.canvas == null)
            {
                return;
            }

            var beganEdit = false;
            try
            {
                graphContext.BeginEdit(false);
                beganEdit = true;
                graphContext.canvas.Cache();
            }
            catch
            {
                // The preview should never break the finder if Visual Scripting cannot cache widgets.
            }
            finally
            {
                if (beganEdit)
                {
                    graphContext.EndEdit();
                }
            }
        }

        private void DrawWidgetPreview(Rect rect, IWidget widget)
        {
            var canvas = widget.canvas;
            var graph = canvas?.graph;
            var previousGraphZoom = graph?.zoom ?? 1f;
            var previousViewport = canvas?.viewport ?? Rect.zero;
            var viewportChanged = false;
            var widgets = GetWidgetTree(widget)
                .OrderBy(w => w.zIndex)
                .ToList();

            var widgetRect = CalculateWidgetPreviewArea(widgets);
            if (widgetRect.width <= 0 || widgetRect.height <= 0)
            {
                var graphElement = widget.item as IGraphElement;
                DrawPreviewMessage(rect, graphElement != null ? GraphElement.GetElementName(graphElement) : "Preview unavailable");
                return;
            }

            var scale = CalculatePreviewZoom(rect, widgetRect);
            scale = ClampPreviewZoom(scale, widget);
            var graphZoomChanged = false;

            try
            {
                if (graph != null && !Mathf.Approximately(graph.zoom, scale))
                {
                    graph.zoom = scale;
                    graphZoomChanged = true;
                    RepositionPreviewWidgets(widgets);
                    canvas.CacheWidgetPositions();
                    widgets = GetWidgetTree(widget)
                        .OrderBy(w => w.zIndex)
                        .ToList();
                    widgetRect = CalculateWidgetPreviewArea(widgets);
                    if (widgetRect.width <= 0 || widgetRect.height <= 0)
                    {
                        var graphElement = widget.item as IGraphElement;
                        DrawPreviewMessage(rect, graphElement != null ? GraphElement.GetElementName(graphElement) : "Preview unavailable");
                        return;
                    }

                    scale = CalculatePreviewZoom(rect, widgetRect);
                    scale = ClampPreviewZoom(scale, widget);
                }

                var canvasArea = CalculatePreviewCanvasArea(rect, scale);
                var scroll = widgetRect.center - (canvasArea.size / 2f);
                if (canvas != null)
                {
                    canvas.viewport = new Rect(scroll, canvasArea.size);
                    viewportChanged = true;
                }

                var previousMatrix = GUI.matrix;
                var previousColor = GUI.color;
                var previousContentColor = GUI.contentColor;
                var previousBackgroundColor = GUI.backgroundColor;

                try
                {
                    GUI.color = Color.white;
                    GUI.contentColor = Color.white;
                    GUI.backgroundColor = Color.white;
                    GUI.matrix = previousMatrix * Matrix4x4.Scale(scale * Vector3.one);

                    GUI.BeginClip(canvasArea, -scroll, Vector2.zero, false);
                    try
                    {
                        foreach (var previewWidget in widgets)
                        {
                            if (Event.current.type == EventType.Repaint || previewWidget.backgroundRequiresInput)
                            {
                                previewWidget.DrawBackground();
                            }
                        }

                        foreach (var previewWidget in widgets)
                        {
                            if (Event.current.type == EventType.Repaint || previewWidget.foregroundRequiresInput)
                            {
                                previewWidget.DrawForeground();
                            }
                        }
                    }
                    finally
                    {
                        GUI.EndClip();
                    }
                }
                finally
                {
                    GUI.matrix = previousMatrix;
                    GUI.color = previousColor;
                    GUI.contentColor = previousContentColor;
                    GUI.backgroundColor = previousBackgroundColor;
                }
            }
            finally
            {
                if (viewportChanged && canvas != null)
                {
                    canvas.viewport = previousViewport;
                }

                if (graphZoomChanged && graph != null)
                {
                    graph.zoom = previousGraphZoom;
                    RepositionPreviewWidgets(widgets);
                    canvas.CacheWidgetPositions();
                }
            }
        }

        private float CalculatePreviewZoom(Rect rect, Rect widgetRect)
        {
            const float overviewPadding = 50f;
            var contentRect = GetPreviewContentRect(rect);
            return Mathf.Min(contentRect.width / (widgetRect.width + overviewPadding), contentRect.height / (widgetRect.height + overviewPadding));
        }

        private float ClampPreviewZoom(float zoom, IWidget widget)
        {
            var minZoom = widget is IUnitWidget ? FlowCanvas.inspectorZoomThreshold : 0.05f;
            return Mathf.Clamp(zoom, minZoom, 1f);
        }

        private Rect CalculatePreviewCanvasArea(Rect rect, float zoom)
        {
            var contentRect = GetPreviewContentRect(rect);
            var canvasArea = contentRect;
            canvasArea.position /= zoom;
            canvasArea.size /= zoom;
            return canvasArea;
        }

        private Rect GetPreviewContentRect(Rect rect)
        {
            const float padding = 2f;
            return new Rect(
                rect.x + padding,
                rect.y + padding,
                Mathf.Max(1f, rect.width - padding * 2f),
                Mathf.Max(1f, rect.height - padding * 2f));
        }

        private void RepositionPreviewWidgets(IEnumerable<IWidget> widgets)
        {
            foreach (var previewWidget in widgets)
            {
                previewWidget.Reposition();
            }
        }

        private IEnumerable<IWidget> GetWidgetTree(IWidget widget)
        {
            yield return widget;

            foreach (var subWidget in widget.subWidgets)
            {
                foreach (var childWidget in GetWidgetTree(subWidget))
                {
                    yield return childWidget;
                }
            }
        }

        private Rect CalculateWidgetPreviewArea(IEnumerable<IWidget> widgets)
        {
            var hasArea = false;
            var area = Rect.zero;

            foreach (var widget in widgets)
            {
                var widgetRect = widget.clippingPosition;
                if (widgetRect.width <= 0 || widgetRect.height <= 0)
                {
                    widgetRect = widget.position;
                }

                if (widgetRect.width <= 0 || widgetRect.height <= 0)
                {
                    continue;
                }

                area = hasArea ? area.Encompass(widgetRect) : widgetRect;
                hasArea = true;
            }

            return area;
        }

        private void DrawPreviewMessage(Rect rect, string message)
        {
            GUI.Label(rect, message, EditorStyles.centeredGreyMiniLabel);
        }

        private Texture2D GetIcon(ResultItem resultItem)
        {
            try
            {
                if (resultItem?.variableDeclaration != null && resultItem.variableKind.HasValue)
                {
                    return (BoltCore.Resources.icons.VariableKind(resultItem.variableKind.Value))[IconSize.Small];
                }

                if (resultItem?.graphElement == null)
                {
                    return resultItem?.editedObject?.GetType().Icon()?[IconSize.Small];
                }

                switch (resultItem.graphElement.GetType().ToString())
                {
                    case "Unity.VisualScripting.GetVariable":
                    case "Bolt.GetVariable":
                        return (BoltCore.Resources.icons.VariableKind(((GetVariable)resultItem.graphElement).kind))[IconSize.Small];
                    case "Unity.VisualScripting.IsVariableDefined":
                    case "Bolt.IsVariableDefined":
                        return (BoltCore.Resources.icons.VariableKind(((IsVariableDefined)resultItem.graphElement).kind))[IconSize.Small];
                    case "Unity.VisualScripting.SetVariable":
                    case "Bolt.SetVariable":
                        return (BoltCore.Resources.icons.VariableKind(((SetVariable)resultItem.graphElement).kind))[IconSize.Small];
                    case "Unity.VisualScripting.GetMember":
                    case "Bolt.GetMember":
                        return ((GetMember)resultItem.graphElement).member.declaringType.Icon()?[IconSize.Small];
                    case "Unity.VisualScripting.SetMember":
                    case "Bolt.SetMember":
                        return ((SetMember)resultItem.graphElement).member.declaringType.Icon()?[IconSize.Small];
                    case "Unity.VisualScripting.InvokeMember":
                    case "Bolt.InvokeMember":
                        return ((InvokeMember)resultItem.graphElement).member.declaringType.Icon()?[IconSize.Small];
                }
            } catch (Exception)
            {
                // let's just ignore this and move on then
                // for now, I get the error on lookinput [getvariable: flow] and [setvariable: flow]
                //Debug.Log($" Could not get icon of {resultItem.graphElement.GetType()} on {GraphElement.GetElementName(resultItem.graphElement)} because of {e.Message} {e.StackTrace}");
            }

            if (resultItem?.graphElement == null)
            {
                return null;
            }

            Type objectType = resultItem.graphElement.GetType();
            var texture = objectType.Icon()?[IconSize.Small];
            return texture;
        }

        private void PerformSearch(string keyword, bool isExact = false) {
            lastSearchExact = isExact;
            searchField.value = keyword;

            if (variableRenameSearchActive)
            {
                PerformVariableRenameSearch();
                return;
            }

            if (eventRenameSearchActive)
            {
                PerformEventRenameSearch();
                return;
            }

            if (enableCurrentGraphSearch.value == true)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(keyword, prefs.stateSearchContext, isExact);
            }
            if (enableAllGraphsSearch.value == true)
            {
                searchItems[UVSFinderTabs.all] = UVSSearchProvider.PerformSearchAll(keyword, isExact);
            }
            if (enableHierarchySearch.value == true)
            {
                searchItems[UVSFinderTabs.hierarchy] = UVSSearchProvider.PerformSearchInHierarchy(keyword, isExact);
            }
        }

        private void PerformVariableRenameSearch()
        {
            lastSearchExact = true;
            searchField.value = variableRenameName;

            if (enableCurrentGraphSearch.value == true)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterVariableRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(variableRenameName, prefs.stateSearchContext, false),
                    variableRenameName,
                    variableRenameKind);
            }

            if (enableAllGraphsSearch.value == true)
            {
                searchItems[UVSFinderTabs.all] = UVSSearchProvider.FilterVariableRenameResults(
                    UVSSearchProvider.PerformSearchAll(variableRenameName, false),
                    variableRenameName,
                    variableRenameKind);
            }

            if (enableHierarchySearch.value == true)
            {
                searchItems[UVSFinderTabs.hierarchy] = UVSSearchProvider.FilterVariableRenameResults(
                    UVSSearchProvider.PerformSearchInHierarchy(variableRenameName, false),
                    variableRenameName,
                    variableRenameKind);
            }
        }

        private void PerformEventRenameSearch()
        {
            lastSearchExact = true;
            searchField.value = eventRenameInfo.ValueText;

            if (enableCurrentGraphSearch.value == true)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterEventRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(eventRenameInfo.ValueText, prefs.stateSearchContext, false),
                    eventRenameInfo);
            }

            if (enableAllGraphsSearch.value == true)
            {
                searchItems[UVSFinderTabs.all] = UVSSearchProvider.FilterEventRenameResults(
                    UVSSearchProvider.PerformSearchAll(eventRenameInfo.ValueText, false),
                    eventRenameInfo);
            }

            if (enableHierarchySearch.value == true)
            {
                searchItems[UVSFinderTabs.hierarchy] = UVSSearchProvider.FilterEventRenameResults(
                    UVSSearchProvider.PerformSearchInHierarchy(eventRenameInfo.ValueText, false),
                    eventRenameInfo);
            }
        }

        public void PerformSearchInCurrent(string keyword, bool isExact = false)
        {
            variableRenameSearchActive = false;
            eventRenameSearchActive = false;
            eventRenameInfo = default;
            lastSearchExact = isExact;
            searchField.value = keyword;
            OnCurrentGraphClick();
            searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext, isExact);
            RefreshReplacePreview();
        }

        private void PerformSearch()
        {
            variableRenameSearchActive = false;
            eventRenameSearchActive = false;
            eventRenameInfo = default;
            PerformSearch(searchField.value, false);
        }

        private void PerformSearchCurrent()
        {
            if (variableRenameSearchActive)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterVariableRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(variableRenameName, prefs.stateSearchContext, false),
                    variableRenameName,
                    variableRenameKind);
            }
            else if (eventRenameSearchActive)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterEventRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(eventRenameInfo.ValueText, prefs.stateSearchContext, false),
                    eventRenameInfo);
            }
            else
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext, lastSearchExact);
            }

            RefreshReplacePreview();
        }

        private void setWindowTitle()
        {
            var wnd = GetWindow<UVSFinder>();
            wnd.titleContent = new GUIContent("Node Finder (" + searchItems[selectedTab]?.Count + ")");
        }

        private void setTabsResults()
        {
            tabAllGraphs.text = ("      All Graphs (" + searchItems[UVSFinderTabs.all]?.Count + ")");
            tabCurrentGraph.text = ("      Current Graph (" + searchItems[UVSFinderTabs.current]?.Count + ")");
            tabHierarchyGraphButton.text = ("      Hierarchy (" + searchItems[UVSFinderTabs.hierarchy]?.Count + ")");
        }
        private void OnCurrentGraphClick()
        {
            tabCurrentGraph.AddToClassList("selected");
            tabAllGraphs.RemoveFromClassList("selected");
            tabHierarchyGraphButton.RemoveFromClassList("selected");
            selectedTab = UVSFinderTabs.current;
            if(searchItems[UVSFinderTabs.current].Count == 0)
            {
                PerformSearchCurrent();
            }
            DisplayResultsItems();
            setWindowTitle();
        }

        private void OnAllGraphsClick()
        {
            tabAllGraphs.AddToClassList("selected");
            tabCurrentGraph.RemoveFromClassList("selected");
            tabHierarchyGraphButton.RemoveFromClassList("selected");
            selectedTab = UVSFinderTabs.all;
            DisplayResultsItems();
            setWindowTitle();
        }

        private void OnHierarchyGraphClick()
        {
            tabHierarchyGraphButton.AddToClassList("selected");
            tabAllGraphs.RemoveFromClassList("selected");
            tabCurrentGraph.RemoveFromClassList("selected");
            selectedTab = UVSFinderTabs.hierarchy;
            DisplayResultsItems();
            setWindowTitle();
        }

        private void OnEnableCurrentGraphSearchValueChanged(ChangeEvent<bool> evt)
        {
            prefs.enableCurrentGraphSearch = enableCurrentGraphSearch.value;
        }
        private void OnEnableAllGraphsSearchValueChanged(ChangeEvent<bool> evt)
        {
            prefs.enableAllGraphsSearch = enableAllGraphsSearch.value;
        }
        private void OnEnableHierarchySearchValueChanged(ChangeEvent<bool> evt)
        {
            prefs.enableHierarchySearch = enableHierarchySearch.value;
        }
        private void OnKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                if (searchField.panel.focusController.focusedElement == searchField)
                {
                    OnSearchAction();
                }
            }
        }

        public void OnItemsChosen(IEnumerable<object> itemsChosen)
        {
            var resultItem = GetPrimarySelectedResultItem(itemsChosen);
            if (resultItem == null)
            {
                return;
            }

            if (!EditorWindow.HasOpenInstances<GraphWindow>())
            {
                GraphWindow.OpenTab();
            }

            SelectElement(resultItem);

            // if we click on an item in the "all graphs" result list,
            // then we need to redo the "current graph" search
            if (selectedTab == UVSFinderTabs.all)
            {
                RefreshCurrentSearchForActiveMode();
            }
            else if (selectedTab == UVSFinderTabs.hierarchy)
            {
                RefreshCurrentSearchForActiveMode();
            }
            GetWindow<UVSFinder>().Focus();

            if (resultItem.gameObject != null)
            {
                EditorGUIUtility.PingObject(resultItem.gameObject);
            }
        }

        private void RefreshCurrentSearchForActiveMode()
        {
            if (variableRenameSearchActive)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterVariableRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(variableRenameName, prefs.stateSearchContext, false),
                    variableRenameName,
                    variableRenameKind);
            }
            else if (eventRenameSearchActive)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.FilterEventRenameResults(
                    UVSSearchProvider.PerformSearchInCurrentScript(eventRenameInfo.ValueText, prefs.stateSearchContext, false),
                    eventRenameInfo);
            }
            else
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext, lastSearchExact);
            }

            RefreshReplacePreview();
            setTabsResults();
        }

        public void OnSearchAction(string keyword, bool isExact = false)
        {
            variableRenameSearchActive = false;
            eventRenameSearchActive = false;
            eventRenameInfo = default;
            lastSearchExact = isExact;
            resultListview.Clear();
            ResetResultItems();
            PerformSearch(keyword, isExact);
            RefreshReplacePreview();
            setWindowTitle();
            setTabsResults();
            DisplayResultsItems();
            searchField.ElementAt(0).Focus();
        }

        private void OnSearchAction()
        {
            OnSearchAction(searchField.value);
        }

        private void ResetResultItems()
        {
            searchItems[UVSFinderTabs.all].Clear();
            searchItems[UVSFinderTabs.current].Clear();
            searchItems[UVSFinderTabs.hierarchy].Clear();
        }

        private void DisplayResultsItems()
        {
            if (resultListview == null)
            {
                return;
            }

            resultListview.itemsSource = searchItems[selectedTab];
#if UNITY_2021_2_OR_NEWER
            resultListview.Rebuild();
#else
            resultListview.Refresh();
#endif
        }

        private void SelectElement(ResultItem resultItem)
        {

            if (!OpenWindow(resultItem))
            {
                PingResultAsset(resultItem);
                return;
            }

            if (resultItem.graphElement == null)
            {
                PingResultAsset(resultItem);
                return;
            }

            var graphWindow = GetWindow<GraphWindow>();
            try
            {
                if (graphWindow.context?.canvas is VisualScriptingCanvas<FlowGraph>)
                {
                    SelectElementInScriptGraph(resultItem);
                }
                else if (graphWindow.context?.canvas is VisualScriptingCanvas<StateGraph>)
                {
                    SelectElementInStateGraph(resultItem);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not select {resultItem.itemName} in graph: {e.Message}");
            }

        }

        private void SelectElementInScriptGraph(ResultItem resultItem)
        {
            //Debug.Log("select item in script graph");
            var graphWindow = GetWindow<GraphWindow>();
            VisualScriptingCanvas<FlowGraph> canvas = (VisualScriptingCanvas<FlowGraph>)graphWindow.context.canvas;
            var panPosition = new Vector2();
            if (resultItem.graphElement.GetType().ToString() == "Unity.VisualScripting.GraphGroup" || resultItem.graphElement.GetType().ToString() == "Bolt.GraphGroup")
            {
                panPosition = new Vector2(((GraphGroup)resultItem.graphElement).position.xMin + (canvas.viewport.width / 2) - 10, ((GraphGroup)resultItem.graphElement).position.yMin + (canvas.viewport.height / 2) - 10);
            }
            else
            {
                panPosition = new Vector2(((Unit)resultItem.graphElement).position.x, ((Unit)resultItem.graphElement).position.y);
            }
            graphWindow.context.graph.zoom = 1f;
            graphWindow.context.graph.pan = panPosition;
            graphWindow.context.selection.Select(resultItem.graphElement);
            //canvas.ViewElements(new List<IGraphElement>(){ resultItem.graphElement });
            //canvas.TweenViewport(panPosition, 1f, 0.5f);
        }

        private void SelectElementInStateGraph(ResultItem resultItem)
        {
            //Debug.Log("select item in state graph");
            var graphWindow = GetWindow<GraphWindow>();
            var canvas = (VisualScriptingCanvas<StateGraph>)graphWindow.context.canvas;
            var searchInfo = FindElementsInStateGraph(resultItem, graphWindow, canvas.graph);

            graphWindow.context.graph.zoom = 1f;
            graphWindow.context.selection.Select(new List<IGraphElement>() { searchInfo.element });
            try {
                canvas.ViewElements(new List<IGraphElement>() { searchInfo.element });
            }
            catch (Exception)
            {
                //Debug.Log("Could not pan to element " + resultItem.graphElement.GetType() + " because of: " + e);
            }
        }

        // find the element, but I also need all the graph references 
        private SearchInfo FindElementsInStateGraph(ResultItem resultItem, GraphWindow graphWindow, StateGraph stateGraph)
        {
            var st = (INesterState)stateGraph.states.FirstOrDefault(s => s.guid.ToString() == resultItem.graphGuid);
            if (st != null)
            {
                // it is one of the states in the first layer. Return the first layer
                return new SearchInfo() { element = st, found = true };
            }

            var t = (INesterStateTransition)stateGraph.transitions.FirstOrDefault(s => s.guid.ToString() == resultItem.graphGuid);
            if (t != null)
            {
                // it is one of the states in the first layer. Return the first layer
                return new SearchInfo() { element = t, found = true };
            }

            foreach (var s in stateGraph.transitions)
            {
                if (s is INesterStateTransition)
                {
                    var nesterState = s as INesterStateTransition;
                    if (nesterState.graph.elements.Count() > 0)
                    {
                        foreach (var e in nesterState.graph.elements)
                        {
                            if (e.guid.ToString() == resultItem.graphGuid)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                return new SearchInfo() { element = e, found = true };
                            }

                            if (e is StateUnit)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                var result = FindElementsInStateUnit(resultItem, graphWindow, (StateUnit)e);
                                if (result.found) { return result; }
                                graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                            }

                            if (e is SubgraphUnit)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                var result = FindElementsInSubGraphUnit(resultItem, graphWindow, (SubgraphUnit)e);
                                if (result.found) { return result; }
                                graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                            }
                        }
                    }
                }
            }

            foreach (var s in stateGraph.states)
            {
                if (s is INesterState)
                {
                    var nesterState = s as INesterState; 
                    if (nesterState.childGraph.elements.Count() > 0)
                    {
                        foreach (var e in nesterState.childGraph.elements)
                        {
                            if (e.guid.ToString() == resultItem.graphGuid)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                return new SearchInfo() { element = e, found = true };
                            }

                            if (e is StateUnit)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                var result = FindElementsInStateUnit(resultItem, graphWindow, (StateUnit)e);
                                if (result.found) { return result; }
                                graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                            }

                            if (e is SubgraphUnit)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                var result = FindElementsInSubGraphUnit(resultItem, graphWindow, (SubgraphUnit)e);
                                if (result.found) { return result; }
                                graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                            }

                            if(e is IStateTransition)
                            {
                                graphWindow.reference = graphWindow.reference.ChildReference(nesterState, false);
                                var result = FindElementsInSubGraphUnit(resultItem, graphWindow, (SubgraphUnit)e);
                                if (result.found) { return result; }
                                graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                            }
                        }
                    }
                }
            }

            // if we reach here, it means that we were not able to find the element...
            return new SearchInfo() { element = resultItem.graphElement, found = false };
        }

        private SearchInfo FindElementsInStateUnit(ResultItem resultItem, GraphWindow graphWindow, StateUnit stateUnit)
        {
            foreach (var ge in stateUnit.graph.elements)
            {
                if (ge.guid.ToString() == resultItem.graphGuid)
                {
                    return new SearchInfo() { element = ge, found = true };
                }
            }

            if (stateUnit.nest?.embed != null)
            {
                // root of the state unit
                foreach (var ne in stateUnit.nest.embed.elements)
                {
                    if (ne.guid.ToString() == resultItem.graphGuid)
                    {
                        graphWindow.reference = graphWindow.reference.ChildReference(stateUnit, true);// go into furet 2
                        return new SearchInfo() {element = ne, found = true };
                    }
                }

                // states of the state unit
                foreach (INesterState es in stateUnit.nest.embed.states)
                {
                    foreach (var se in es.childGraph.elements)
                    {
                        if (se.guid.ToString() == resultItem.graphGuid)
                        {
                            graphWindow.reference = graphWindow.reference.ChildReference(stateUnit, true);// go into furet 2
                            graphWindow.reference = graphWindow.reference.ChildReference(es, true);// go into furet 3
                            return new SearchInfo() { element = se, found = true };
                        }

                        if (se is StateUnit)
                        {
                            graphWindow.reference = graphWindow.reference.ChildReference(stateUnit, true);// go into furet 2
                            graphWindow.reference = graphWindow.reference.ChildReference(es, false);
                            var result = FindElementsInStateUnit(resultItem, graphWindow, (StateUnit)se);
                            if (result.found) { return result; }
                            graphWindow.reference = graphWindow.reference.ParentReference(false).ParentReference(false);// move back up instead
                        }

                        if (se is SubgraphUnit)
                        {
                            graphWindow.reference = graphWindow.reference.ChildReference(stateUnit, true);// go into furet 2
                            graphWindow.reference = graphWindow.reference.ChildReference(es, false);
                            var result = FindElementsInSubGraphUnit(resultItem, graphWindow, (SubgraphUnit)se);
                            if (result.found) { return result; }
                            graphWindow.reference = graphWindow.reference.ParentReference(false).ParentReference(false);// move back up instead
                        }
                    }
                }
            }
            // if we reach here, it means that we were not able to find the element...
            return new SearchInfo() { element = resultItem.graphElement, found = false };
        }

        private SearchInfo FindElementsInSubGraphUnit(ResultItem resultItem, GraphWindow graphWindow, SubgraphUnit subgraphUnit)
        {
            foreach (var e in subgraphUnit.nest.graph.elements)
            {
                if (e.guid.ToString() == resultItem.graphGuid)
                {
                    graphWindow.reference = graphWindow.reference.ChildReference(subgraphUnit, false);
                    return new SearchInfo() { element = e, found = true };
                }

                if (e is StateUnit)
                {
                    graphWindow.reference = graphWindow.reference.ChildReference(subgraphUnit, false);
                    var result = FindElementsInStateUnit(resultItem, graphWindow, (StateUnit)e);
                    if (result.found) { return result; }
                    graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                }

                if (e is SubgraphUnit)
                {
                    graphWindow.reference = graphWindow.reference.ChildReference(subgraphUnit, false);
                    var result = FindElementsInSubGraphUnit(resultItem, graphWindow, (SubgraphUnit)e);
                    if (result.found) { return result; }
                    graphWindow.reference = graphWindow.reference.ParentReference(false);// move back up instead
                }
            }
            // if we reach here, it means that we were not able to find the element...
            return new SearchInfo() { element = resultItem.graphElement, found = false };
        }

        private bool OpenWindow(ResultItem resultItem)
        {
            //Debug.Log($"Focusing in asset {graphItem.assetPath}, on {graphItem.itemName}");
            if (TryResolveGraphReference(resultItem, out var graphReference))
            {
                try
                {
                    GraphWindow.OpenActive(graphReference);
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"Could not open graph for {resultItem.itemName}: {e.Message}");
                }
            }

            return false;
        }

        private bool TryResolveGraphReference(ResultItem resultItem, out GraphReference graphReference)
        {
            graphReference = null;

            if (resultItem == null)
            {
                return false;
            }

            if (TryResolvePrefabGraphReference(resultItem, out graphReference))
            {
                return true;
            }

            if (TryUseGraphReference(resultItem.graphReference, out graphReference))
            {
                resultItem.graphReference = graphReference;
                return true;
            }

            if (TryResolveMacroGraphReference(resultItem, out graphReference))
            {
                return true;
            }

            return false;
        }

        private bool TryUseGraphReference(GraphReference sourceReference, out GraphReference graphReference)
        {
            graphReference = null;

            if (sourceReference == null)
            {
                return false;
            }

            try
            {
                graphReference = sourceReference.isValid ? sourceReference : sourceReference.Revalidate(false);
                return graphReference != null && graphReference.isValid;
            }
            catch
            {
                graphReference = null;
                return false;
            }
        }

        private bool TryResolvePrefabGraphReference(ResultItem resultItem, out GraphReference graphReference)
        {
            graphReference = null;

            if (string.IsNullOrEmpty(resultItem.assetPath) || Path.GetExtension(resultItem.assetPath) != ".prefab")
            {
                return false;
            }

            var prefabStage = PrefabStageUtility.OpenPrefab(resultItem.assetPath);
            var prefabRoot = prefabStage?.prefabContentsRoot;
            if (prefabRoot == null)
            {
                return false;
            }

            foreach (var scriptMachine in prefabRoot.GetComponentsInChildren<ScriptMachine>(true))
            {
                if (TryFindReferenceInRoot(scriptMachine, resultItem, out graphReference))
                {
                    return true;
                }
            }

            foreach (var stateMachine in prefabRoot.GetComponentsInChildren<StateMachine>(true))
            {
                if (TryFindReferenceInRoot(stateMachine, resultItem, out graphReference))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryResolveMacroGraphReference(ResultItem resultItem, out GraphReference graphReference)
        {
            graphReference = null;

            if (string.IsNullOrEmpty(resultItem.assetPath))
            {
                return false;
            }

            Type t = AssetDatabase.GetMainAssetTypeAtPath(resultItem.assetPath);
            if (t == typeof(ScriptGraphAsset))
            {
                var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(resultItem.assetPath);
                if (sga == null)
                {
                    return false;
                }

                graphReference = GraphReference.New(sga, true);
                return true;
            }

            if (t == typeof(StateGraphAsset))
            {
                var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(resultItem.assetPath);
                if (sga == null)
                {
                    return false;
                }

                graphReference = GraphReference.New(sga, true);
                return true;
            }

            return false;
        }

        private bool TryFindReferenceInRoot(IGraphRoot root, ResultItem resultItem, out GraphReference graphReference)
        {
            graphReference = null;

            if (root?.childGraph == null)
            {
                return false;
            }

            GraphReference rootReference;
            try
            {
                rootReference = root.GetReference().AsReference();
            }
            catch
            {
                return false;
            }

            if (!TryFindElementReference(rootReference, rootReference.graph, resultItem.graphGuid, out graphReference, out var graphElement))
            {
                return false;
            }

            resultItem.graphReference = graphReference;
            resultItem.graphElement = graphElement;
            resultItem.itemName = GraphElement.GetElementName(graphElement);
            return true;
        }

        private bool TryFindElementReference(GraphReference reference, IGraph graph, string graphGuid, out GraphReference graphReference, out IGraphElement graphElement)
        {
            graphReference = null;
            graphElement = null;

            if (reference == null || graph == null || string.IsNullOrEmpty(graphGuid))
            {
                return false;
            }

            graphElement = graph.elements.FirstOrDefault(e => e.guid.ToString() == graphGuid);
            if (graphElement != null)
            {
                graphReference = reference;
                return true;
            }

            foreach (var parentElement in graph.elements.OfType<IGraphParentElement>())
            {
                if (parentElement.childGraph == null)
                {
                    continue;
                }

                GraphReference childReference;
                try
                {
                    childReference = reference.ChildReference(parentElement, false);
                }
                catch
                {
                    continue;
                }

                if (TryFindElementReference(childReference, parentElement.childGraph, graphGuid, out graphReference, out graphElement))
                {
                    return true;
                }
            }

            return false;
        }

        private void PingResultAsset(ResultItem resultItem)
        {
            if (resultItem?.editedObject != null)
            {
                EditorGUIUtility.PingObject(resultItem.editedObject);
                return;
            }

            if (resultItem?.gameObject != null)
            {
                EditorGUIUtility.PingObject(resultItem.gameObject);
                return;
            }

            if (!string.IsNullOrEmpty(resultItem?.assetPath))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(resultItem.assetPath);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                }
            }
        }
    }

    class SearchInfo {
        public List<GraphReference> references;
        public IGraphElement element;
        public bool found;
    }
}
