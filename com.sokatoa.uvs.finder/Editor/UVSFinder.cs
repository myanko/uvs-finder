using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityObject = UnityEngine.Object;
using System.Linq;
using System.IO;

namespace Unity.VisualScripting.UVSFinder
{
    [Serializable]
    public class UVSFinder : EditorWindow
    {
        internal static UVSFinderPreferences prefs => UVSFinderSettingsProvider.Preferences;
        
        [MenuItem("Tools/VisualScripting/UVS Find in All Graphs %#f")]
        public static void ShowUVSFinder()
        {
            UVSFinder wnd = GetWindow<UVSFinder>();
            wnd.titleContent = new GUIContent("UVSFinder");
        }

        public int searchCount = 0;
        public List<string> nodeList = new List<string>();
        public TextField searchField;
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
        private Button tabAllGraphs;
        private Button tabHierarchyGraphButton;
        private UVSFinderTabs selectedTab;

        public void OnEnable()
        {
            var path = new UVSFinderPaths().findRootPackagePath();
            Debug.Log(path);
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // VisualElements objects can contain other VisualElement following a tree hierarchy.
            /*VisualElement label = new Label("Hello World! From C#");
            root.Add(label);*/

            // Import UXML
            var visualTree =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path+"/UI/UVSFinder.uxml");
            VisualElement UVSFinderFromUXML = visualTree.CloneTree();
            root.Add(UVSFinderFromUXML);

            //Register Keys Events
            root.RegisterCallback<KeyUpEvent>(OnKeyUp, TrickleDown.TrickleDown);

            // Get a reference to the Button from UXML and assign it its action.
            searchField = root.Q<TextField>("search-field");
            var nodePathLabel = root.Q<Label>("node-path-label");
            var searchOptions = root.Q<Button>("searchOptions");
            //listItem
            var listItem = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path+"/UI/listItem.uxml");

            // Tabs
            tabCurrentGraph = root.Q<Button>("currentGraphButton");
            tabCurrentGraph.clicked += OnCurrentGraphClick;
            tabAllGraphs = root.Q<Button>("allGraphsButton");
            tabAllGraphs.clicked += OnAllGraphsClick;
            tabHierarchyGraphButton = root.Q<Button>("hierarchyGraphButton");
            tabHierarchyGraphButton.clicked += OnHierarchyGraphClick;
            selectedTab = UVSFinderTabs.all;

            // The "makeItem" function will be called as needed
            // when the ListView needs more items to render
            Func<VisualElement> makeItem = () => listItem.Instantiate();

            // As the user scrolls through the list, the ListView object
            // will recycle elements created by the "makeItem"
            // and invoke the "bindItem" callback to associate
            // the element with the matching data item (specified as an index in the list)
            Action<VisualElement, int> bindItem = (e, i) =>
            {
                //TODO: Case Current/All/FindReplace/Other
                if ((selectedTab == UVSFinderTabs.current && prefs.showTypeIconCurrent) ||
                    (selectedTab == UVSFinderTabs.all && prefs.showTypeIconAll))
                {
                    var icon = GetIcon(searchItems[selectedTab][i]);
                    var typeIcon = e.Q<Label>("Icon");
                    var sb = new StyleBackground(icon);
                    typeIcon.style.backgroundImage = sb;
                }
                var description = e.Q<VisualElement>("Description");
                if (!description.ClassListContains("highlightdone"))
                {
                    //process only once
                    TextHighlighter.HighlightTextBasedOnQuery(description, searchItems[selectedTab][i].itemName, searchField.text);
                    description.AddToClassList("highlightdone");
                }

                var filePath = e.Q<Label>("FilePath");
                filePath.text = searchItems[selectedTab][i].assetPath;
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
        }

        private Texture2D GetIcon(ResultItem resultItem)
        {
            if (resultItem.graphElement.type.EndsWith(".SetVariable") || resultItem.graphElement.type.EndsWith(".GetVariable"))
            {
                return (BoltCore.Resources.icons.VariableKind(resultItem.graphElement.kind))[IconSize.Small];
            }

            //find the type full name with assembly
            Type objectType = (from asm in AppDomain.CurrentDomain.GetAssemblies()
                               from type in asm.GetTypes()
                               where type.IsClass && type.Name == ((GraphElement)resultItem.graphElement).GetElementType().Split('.').Last()
                               select type).FirstOrDefault();
            //Debug.Log(((GraphElement)searchItems[i].graphElement).type + " = " + objectType);
            var texture = objectType.Icon()?[IconSize.Small];
            return texture;
        }

        private void PerformSearch()
        {
            searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.text);
            searchItems[UVSFinderTabs.all] = UVSSearchProvider.PerformSearchAll(searchField.text);
        }

        private void setWindowTitle()
        {
            var wnd = GetWindow<UVSFinder>();
            wnd.titleContent = new GUIContent("UVSFinder (" + searchItems[selectedTab]?.Count + ")");
        }

        private void setTabsResults()
        {
            tabAllGraphs.text = ("All Graphs (" + searchItems[UVSFinderTabs.all]?.Count + ")");
            tabCurrentGraph.text = ("Current Graph (" + searchItems[UVSFinderTabs.current]?.Count + ")");
        }
        private void OnCurrentGraphClick()
        {
            tabCurrentGraph.AddToClassList("selected");
            tabAllGraphs.RemoveFromClassList("selected");
            tabHierarchyGraphButton.RemoveFromClassList("selected");
            selectedTab = UVSFinderTabs.current;
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

            SelectElement((ResultItem)resultListview.selectedItem);

            // if we click on an item in the "all graphs" result list,
            // then we need to redo the "current graph" search
            if (selectedTab == UVSFinderTabs.all)
            {
                searchItems[UVSFinderTabs.current] = UVSSearchProvider.PerformSearchInCurrentScript(searchField.text);
                setTabsResults();
            }
            GetWindow<UVSFinder>().Focus();
        }

        private void OnSearchAction()
        {
            ResetResultItems();
            PerformSearch();
            setWindowTitle();
            setTabsResults();
            DisplayResultsItems();
            searchField.ElementAt(0).Focus();
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
            resultListview.Refresh();
        }

        private void SelectElement(ResultItem resultItem)
        {
            // no asset path set means we are searching in the current graph,
            // so no need to open it
            if (!String.IsNullOrEmpty(resultItem.assetPath))
            {
                OpenWindow(resultItem);
            }

            if (resultItem.type == typeof(ScriptGraphAsset)) //script graph
            {
                SelectElementInScriptGraph(resultItem);
            }
            else // state graph
            {
                SelectElementInStateGraph(resultItem);
            }

        }

        private void SelectElementInScriptGraph(ResultItem resultItem)
        {
            var graphWindow = GetWindow<GraphWindow>();
            VisualScriptingCanvas<FlowGraph> canvas = (VisualScriptingCanvas<FlowGraph>)graphWindow.context.canvas;
            // pick up the "real element" in the canvas directly
            // because my GraphElement class cannot decorate the Widget for some reason...
            var realElement = canvas.graph.elements.Where(i => i.guid == resultItem.graphElement.guid).FirstOrDefault();
            var panPosition = new Vector2();
            if (resultItem.graphElement.type == "Unity.VisualScripting.GraphGroup" || resultItem.graphElement.type == "Bolt.GraphGroup")
            {
                var realGroup = canvas.graph.groups.Where(u => u.guid == resultItem.graphElement.guid).FirstOrDefault();
                panPosition = new Vector2(realGroup.position.xMin + (canvas.viewport.width/2) - 10, realGroup.position.yMin + (canvas.viewport.height/2) - 10);
            }
            else
            {
                var realNode = canvas.graph.units.Where(u => u.guid == resultItem.graphElement.guid).FirstOrDefault();
                if(realNode != null) panPosition = new Vector2(realNode.position.x, realNode.position.y);
            }
            graphWindow.context.selection.Select(realElement);
            graphWindow.context.graph.zoom = 1f;
            graphWindow.context.graph.pan = panPosition;
            //canvas.ViewElements(new[] { realElement });
            //canvas.TweenViewport(panPosition, 1f, 0.5f);
        }
        private void SelectElementInStateGraph(ResultItem resultItem)
        {
            var graphWindow = GetWindow<GraphWindow>();
            var canvas = (VisualScriptingCanvas<StateGraph>)graphWindow.context.canvas;
            var substateRef = FindSubStateReference(resultItem, graphWindow, canvas);
            OpenSubStateWindow(substateRef);

            // pick up the "real element" in the canvas directly or child graphs
            // because my GraphElement class cannot decorate the Widget for some reason...
            //I suck at linq
            //IGraphElement realElement = canvas.graph.states.Where(s => ((INesterState)s).childGraph.elements.Select(e => e).Where(e => e.guid.ToString() == resultItem.guid)));
            IGraphElement realElement = null;
            foreach (INesterState s in canvas.graph.states)
            {
                foreach(IGraphElement e in s.childGraph.elements)
                {
                    if(e.guid.ToString() == resultItem.guid)
                    {
                        realElement = e;
                        break;
                    }
                }
                if (realElement != null) { break; }
            }
            if (realElement == null)
            {
                foreach (INesterStateTransition s in canvas.graph.transitions)
                {
                    foreach (IGraphElement e in s.childGraph.elements)
                    {
                        if (e.guid.ToString() == resultItem.guid)
                        {
                            realElement = e;
                            break;
                        }
                    }
                    if (realElement != null) { break; }
                }
            }
            if (realElement == null)
            {
                realElement = canvas.graph.groups.Where(u => u.guid == resultItem.graphElement.guid).FirstOrDefault();
            }

            if (realElement != null)
            {
                var panPosition = new Vector2();
                if (resultItem.graphElement.type == "Unity.VisualScripting.GraphGroup" || resultItem.graphElement.type == "Bolt.GraphGroup")
                {
                    panPosition = new Vector2(((GraphGroup)realElement).position.xMin + (canvas.viewport.width / 2) - 10, ((GraphGroup)realElement).position.yMin + (canvas.viewport.height / 2) - 10);
                } else
                {
                    panPosition = new Vector2(resultItem.graphElement.position.x, resultItem.graphElement.position.y);
                }
                graphWindow.context.selection.Select(realElement);
                graphWindow.context.graph.zoom = 1f;
                graphWindow.context.graph.pan = panPosition;
                //canvas.TweenViewport(panPosition, 1f, 0.5f);
            } else
            {
                Debug.Log("Could not find the real element in the state graph's first embed level");
            }
        }

        private GraphReference FindSubStateReference(ResultItem resultItem, GraphWindow graphWindow, VisualScriptingCanvas<StateGraph> canvas)
        {
            //TODO: search in sub-subgraphs (make this recursive)
            // sub graphs contains states and transitions
            // states can use INesterState
            //graph.elements = graph.states + graph.transitions
            var state = (INesterState)canvas.graph.states.FirstOrDefault(s => ((INesterState)s).childGraph.elements.FirstOrDefault(e => e.guid.ToString() == resultItem.guid) != null);
            if (state == null)
            {
                var transitionstate = (INesterStateTransition)canvas.graph.transitions.FirstOrDefault(s => ((INesterStateTransition)s).childGraph.elements.FirstOrDefault(e => e.guid.ToString() == resultItem.guid) != null);
                if(transitionstate != null) return graphWindow.reference.ChildReference(transitionstate, false);

            }
            return graphWindow.reference.ChildReference(state, false);
        }

        private void OpenSubStateWindow(GraphReference substateReference)
        {
            var graphWindow = GetWindow<GraphWindow>();
            graphWindow.reference = substateReference; 
        }

        private void OpenWindow(ResultItem resultItem)
        {
            //Debug.Log($"Focusing in asset {graphItem.assetPath}, on {graphItem.itemName}");
            UnityObject unityObject = AssetDatabase.LoadAssetAtPath(resultItem.assetPath, typeof(UnityObject));
            GraphReference graphReference;
            if (resultItem.type == typeof(ScriptGraphAsset))
            {
                ScriptGraphAsset scriptGraphAsset = unityObject as ScriptGraphAsset;
                graphReference = scriptGraphAsset.GetReference().AsReference();
                //graphReference = GraphReference.New(scriptGraphAsset, true);
            } else
            {
                StateGraphAsset stateGraphAsset = unityObject as StateGraphAsset;
                graphReference = GraphReference.New(stateGraphAsset, true);
            }
            // open the window
            GraphWindow.OpenActive(graphReference);
        }
    }
}
