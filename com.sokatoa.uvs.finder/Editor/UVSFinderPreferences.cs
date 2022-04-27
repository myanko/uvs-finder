﻿using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.VisualScripting.UVSFinder
{
    internal class UVSFinderPreferences : ScriptableObject
    {
        public Color testHighLightColor = new Color(255.0f,128.0f,0.0f, 1.0f);
        //Find in Current Graph
        public bool showTypeIconCurrent = true;
        public bool ShowAllNodesInGraphCurrent = true;
        public ItemAction itemClickCurrent = ItemAction.OpenInGraph;
        public ItemAction itemDoubleClickCurrent = ItemAction.OpenAndClose;
        //Find in all Graphs
        public bool showTypeIconAll = true;
        public bool ShowGraphPathAll = true;
        public bool ShowAllNodesInProjectAll = true;
        public ItemAction itemClickAll = ItemAction.OpenInGraph;
        public ItemAction itemDoubleClickAll = ItemAction.OpenAndClose;
        //Find in Hierarchy
        public bool showTypeIconHierarchy = true;
    }

    internal enum ItemAction
    {
        [InspectorName("Nothing")]
        [Tooltip("Item will only be selected in the list.")]
        Nothing,
        [InspectorName("Open in graph")]
        [Tooltip("After doing the action, the graph window window will open with the item focused on.")]
        OpenInGraph,
        [InspectorName("Open in graph and close the finder")]
        [Tooltip("After doing the action, the UVS Finder window will close")]
        OpenAndClose
    }
    
}