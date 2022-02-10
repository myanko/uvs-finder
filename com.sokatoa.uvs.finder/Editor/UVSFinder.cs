using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;

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
            switch (resultItem.graphElement.GetType().ToString())
            {
                case "Unity.VisualScripting.GetVariable":
                    return (BoltCore.Resources.icons.VariableKind(((GetVariable)resultItem.graphElement).kind))[IconSize.Small];
                case "Unity.VisualScripting.SetVariable":
                    return (BoltCore.Resources.icons.VariableKind(((SetVariable)resultItem.graphElement).kind))[IconSize.Small];
                    /*case "Bolt.GetVariable":
                    case "Bolt.SetVariable":*/
            }

            //find the type full name with assembly
            Type objectType = (from asm in AppDomain.CurrentDomain.GetAssemblies()
                               from type in asm.GetTypes()
                               where type.IsClass && type.Name == resultItem.graphElement.GetType().ToString().Split('.').Last()
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
            resultListview.Clear();
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
            //Debug.Log("select item in script graph");
            var graphWindow = GetWindow<GraphWindow>();
            VisualScriptingCanvas<FlowGraph> canvas = (VisualScriptingCanvas<FlowGraph>)graphWindow.context.canvas;
            var panPosition = new Vector2();
            if (resultItem.graphElement.GetType().ToString() == "Unity.VisualScripting.GraphGroup" || resultItem.graphElement.GetType().ToString() == "Bolt.GraphGroup")
            {
                panPosition = new Vector2(((GraphGroup)resultItem.graphElement).position.xMin + (canvas.viewport.width/2) - 10, ((GraphGroup)resultItem.graphElement).position.yMin + (canvas.viewport.height/2) - 10);
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
            var substateRef = FindSubStateReferenceAndElement(resultItem, graphWindow, canvas);
            if (substateRef != null)
            {
                OpenSubStateWindow(substateRef.Item1);
            } else
            {
                Debug.Log("no substate to open!");
            }
            
            //if (resultItem.graphElement is Unit && !(resultItem.graphElement is StateUnit))
            //{
            try
            {
                // the select does not work when the object is not visible (the substate is not properly selected)
                // and it spams the console even though I try catch it...
                // unless I use widget(graphelement).canselect
                graphWindow.context.graph.zoom = 1f;
                //graphWindow.context.selection.Select(new List<IGraphElement>() { resultItem.graphElement }.Where(element => canvas.Widget(element).canSelect));
                // select the canvas' element instead of just any element
                graphWindow.context.selection.Select(new List<IGraphElement>() { substateRef.Item2 });
                canvas.ViewElements(new List<IGraphElement>() { substateRef.Item2 });
            }
            catch (Exception e)
            {
                Debug.Log("Could not pan to element " + resultItem.graphElement.GetType() + " because of: " + e);
            }
            //}
        }

        private Tuple<GraphReference, IGraphElement> FindSubStateReferenceAndElement(ResultItem resultItem, GraphWindow graphWindow, VisualScriptingCanvas<StateGraph> canvas)
        {
            var st = (INesterState)canvas.graph.states.FirstOrDefault(s => s.guid.ToString() == resultItem.guid);
            if (st != null)
            {
                // it is one of the states in the first layer. Return the first layer
                return new Tuple<GraphReference, IGraphElement>(graphWindow.reference, st);
            }
            var g = canvas.graph.groups.FirstOrDefault(g => g.guid.ToString() == resultItem.guid);
            if (g != null)
            {
                // it is one of the states in the first layer. Return the first layer
                return new Tuple<GraphReference, IGraphElement>(graphWindow.reference, g);
            }
            foreach (INesterState s in canvas.graph.states)
            {
                if (s.childGraph.elements.Count() > 0)
                {
                    foreach (var e in s.childGraph.elements)
                    {
                        if (e.guid.ToString() == resultItem.guid)
                        {
                            return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference(s, false), e);
                        }

                        if (e is StateUnit)
                        {
                            foreach (var ge in ((StateUnit)e).graph.elements)
                            {
                                if (ge.guid.ToString() == resultItem.guid)
                                {
                                    return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference(s, false), ge);
                                }
                            }

                            if (((StateUnit)e).nest?.embed != null)
                            {
                                // root of the state unit
                                foreach (var ne in ((StateUnit)e).nest.embed.elements)
                                {
                                    if (ne.guid.ToString() == resultItem.guid)
                                    {
                                        // TODO: fix this! figure out how to pass the root of a state unit's states
                                        var rootState = (INesterState)((StateUnit)e).nest.embed.states.First();
                                        if (rootState != null)
                                        {
                                            return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference(rootState, false), rootState);
                                        }
                                        return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference((StateUnit)e, false), ne);//(INesterState)((StateUnit)e).nest.embed, false);
                                    }
                                }

                                // states of the state unit
                                foreach (INesterState es in ((StateUnit)e).nest.embed.states)
                                {
                                    foreach (var se in es.graph.elements)
                                    {
                                        if (se.guid.ToString() == resultItem.guid)
                                        {
                                            // I need to figure out the state I am in from the sub element here...
                                            // only the canvas seems to know about the states... even though I am in a stateunit
                                            return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference(es, false), se);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var transitionstate = (INesterStateTransition)canvas.graph.transitions.FirstOrDefault(s => ((INesterStateTransition)s).childGraph.elements.FirstOrDefault(e => e.guid.ToString() == resultItem.guid) != null);
            if (transitionstate != null) return new Tuple<GraphReference, IGraphElement>(graphWindow.reference.ChildReference(transitionstate, false), transitionstate);

            // if we reach here, it means that we were not able to find the element...
            //Debug.Log("oh no");
            return new Tuple<GraphReference, IGraphElement>(graphWindow.reference, resultItem.graphElement);
        }

        // return the parent of the childgraph containing the element
        private static IGraphParentElement FindSubStateInState(IState state, string guid)
        {
            if (state.guid.ToString() == guid) {
                //Debug.Log("guid is the state itself");
                return (INesterState)state;
            }

            foreach (var e in ((INesterState)state).childGraph.elements)
            {
                if (e.guid.ToString() == guid)
                {
                    //Debug.Log($"guid in the childgraph elements {((INesterState)state).childGraph}");
                    return (IGraphParentElement)((INesterState)state).childGraph;
                }


                if (e is INesterStateTransition)
                {
                    if (e.guid.ToString() == guid)
                    {
                        //Debug.Log("guid is a state transition");
                        return (INesterState)e;
                    }
                    //return FindSubStateInStateTransition((INesterStateTransition)e, guid);
                }

                if (e is INesterState)
                {
                    //Debug.Log("recurse");
                    return FindSubStateInState((INesterState)e, guid);
                }
            }
            

            return null;
        }

        private static INesterState FindSubStateInStateTransition(IStateTransition transition, string guid)
        {
            if (transition.guid.ToString() == guid)
            {
                return (INesterState)transition;
            }
            /*foreach (var e in ((INesterState)transition).childGraph.elements)
            {
                if (e.guid.ToString() == guid)
                {
                    return (INesterState)transition;
                }

                if (e is INesterStateTransition)
                {
                    return FindSubStateInStateTransition((INesterStateTransition)e, guid);
                }
            }*/

            return null;
        }

        private void OpenSubStateWindow(GraphReference substateReference)
        {
            var graphWindow = GetWindow<GraphWindow>();
            graphWindow.reference = substateReference; 
        }

        private void OpenWindow(ResultItem resultItem)
        {
            //Debug.Log($"Focusing in asset {graphItem.assetPath}, on {graphItem.itemName}");
            GraphReference graphReference;
            if (resultItem.type == typeof(ScriptGraphAsset))
            {
                var sga = AssetDatabase.LoadAssetAtPath<ScriptGraphAsset>(resultItem.assetPath);
                //sga.graph.Instantiate(sga.GetReference().AsReference());
                graphReference = sga.GetReference().AsReference();
                //graphReference = GraphReference.New(sga.GetReference().root, true);
            } 
            else
            {
                var sga = AssetDatabase.LoadAssetAtPath<StateGraphAsset>(resultItem.assetPath);
                //graphReference = GraphReference.New(sga.GetReference().root, true);
                graphReference = sga.GetReference().AsReference();
            }
            // open the window
            GraphWindow.OpenActive(graphReference);
        }
    }
}
