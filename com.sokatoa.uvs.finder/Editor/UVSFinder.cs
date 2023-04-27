using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using UnityObject = UnityEngine.Object;
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
                var description = e.Q<VisualElement>("Description");
                if (description.ClassListContains("highlightdone"))
                {
                    // clean up the text and redo it
                    description.hierarchy.Clear();
                }

                TextHighlighter.HighlightTextBasedOnQuery(description, searchItems[selectedTab][i].itemName, searchField.value);
                description.AddToClassList("highlightdone");
                OverwriteHighlightColor(e);

                var filePath = e.Q<Label>("FilePath");
                filePath.text = searchItems[selectedTab][i].assetPath;
                if (!String.IsNullOrEmpty(searchItems[selectedTab][i].stateName))
                {
                    filePath.text += $" > {searchItems[selectedTab][i].stateName}";
                }
                if (searchItems[selectedTab][i].gameObject != null)
                {
                    filePath.text += $" ({searchItems[selectedTab][i].gameObject.name})";
                }
            };

            resultListview = root.Q<ListView>("results-list");
            resultListview.makeItem = makeItem;
            resultListview.bindItem = bindItem;
            resultListview.itemsSource = searchItems[selectedTab];

            // Callback invoked when the user double clicks an item
            resultListview.onItemsChosen += OnItemsChosen;

            // Callback invoked when the user changes the selection inside the ListView
            resultListview.onSelectionChange += OnItemsChosen;
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
        private Texture2D GetIcon(ResultItem resultItem)
        {
            try
            {
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

            Type objectType = resultItem.graphElement.GetType();
            var texture = objectType.Icon()?[IconSize.Small];
            return texture;
        }

        private void PerformSearch(string keyword, bool isExact = false) {
            searchField.value = keyword;
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

        public void PerformSearchInCurrent(string keyword, bool isExact = false)
        {
            searchField.value = keyword;
            OnCurrentGraphClick();
            searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext, isExact);
        }

        private void PerformSearch()
        {
            PerformSearch(searchField.value, false);
        }

        private void PerformSearchCurrent()
        {
            searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext);
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
            if (!EditorWindow.HasOpenInstances<GraphWindow>())
            {
                GraphWindow.OpenTab();
            }

            var resultItem = (ResultItem)resultListview.selectedItem;
            SelectElement(resultItem);

            // if we click on an item in the "all graphs" result list,
            // then we need to redo the "current graph" search
            if (selectedTab == UVSFinderTabs.all)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext);
                setTabsResults();
            }
            else if (selectedTab == UVSFinderTabs.hierarchy)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.value, prefs.stateSearchContext);
                setTabsResults();
            }
            GetWindow<UVSFinder>().Focus();

            if (resultItem.gameObject != null)
            {
                EditorGUIUtility.PingObject(resultItem.gameObject);
            }
        }

        public void OnSearchAction(string keyword, bool isExact = false)
        {
            resultListview.Clear();
            ResetResultItems();
            PerformSearch(keyword, isExact);
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
            resultListview.itemsSource = searchItems[selectedTab];
#if UNITY_2021_2_OR_NEWER
            resultListview.Rebuild();
#else
            resultListview.Refresh();
#endif
        }

        private void SelectElement(ResultItem resultItem)
        {

            OpenWindow(resultItem);

            var graphWindow = GetWindow<GraphWindow>();
            if (graphWindow.context.canvas is VisualScriptingCanvas<FlowGraph>) { 
                SelectElementInScriptGraph(resultItem);
            } 
            else // state graph
            {
                SelectElementInStateGraph(resultItem);
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

        private void OpenWindow(ResultItem resultItem)
        {
            //Debug.Log($"Focusing in asset {graphItem.assetPath}, on {graphItem.itemName}");
            if (resultItem.graphReference != null)
            {
                GraphWindow.OpenActive(resultItem.graphReference);
                return;
            } 
            else if (!String.IsNullOrEmpty(resultItem.assetPath))
            {
                GraphReference graphReference;
                Type t = AssetDatabase.GetMainAssetTypeAtPath(resultItem.assetPath);
                if (t == typeof(ScriptGraphAsset))
                {
                    var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(resultItem.assetPath);
                    graphReference = GraphReference.New(sga, true);
                }
                else
                {
                    var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(resultItem.assetPath);
                    graphReference = GraphReference.New(sga, true);
                }
                // open the window
                GraphWindow.OpenActive(graphReference);
            }
        }
    }

    class SearchInfo {
        public List<GraphReference> references;
        public IGraphElement element;
        public bool found;
    }
}
