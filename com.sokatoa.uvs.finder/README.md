# Node Finder for Unity Visual Scripting V0.5

Node Finder adds project-wide search tools to Unity Visual Scripting. It helps you find units, events, variables, groups, subgraphs, state graphs, and embedded graph content without manually opening every graph in your project.

Open it with **Alt+F**, type a search term, and press **Enter**.

## Features

- Search the current graph, all project graphs, or the open scene hierarchy.
- Search inside embedded script graphs, embedded state graphs, subgraphs, state units, and transition graphs.
- Control current-graph search depth from preferences: current graph, children, parent, or all.
- Enable or disable each search scope directly from the finder tabs.
- Focus and select matching nodes in the Visual Scripting graph window.
- Multi-select results in the finder list.
- Find groups by title and comments.
- Find custom events and matching trigger custom event nodes.
- Find variable nodes by name and kind.
- Find Time nodes by configured input values such as delay, duration, and unscaled time.
- Find graph, object, scene, application, saved, and prefab blackboard variable declarations.
- Right-click graph elements for contextual finder actions.
- Right-click nodes to open the C# script that defines the unit, when Unity can resolve it.
- Right-click variable nodes to rename matching Get, Set, Has, and blackboard variables across the selected search scope.
- Preview graph nodes before replacing values or replacing nodes.
- Replace editable node values in selected results or all results in the active tab.
- Replace whole flow nodes using a selected node as a template.

## Context Menu Actions

Node Finder adds right-click actions directly in Visual Scripting graphs.

Supported contextual actions include:

- Find selected node or group.
- Find related Custom Event and Trigger Custom Event nodes.
- Find related Get Variable, Set Variable, and Has Variable nodes.
- Open the C# script for the clicked unit, when a matching script asset is available.
- Rename variables from Get Variable, Set Variable, and Has Variable nodes.
- Start find and replace from the current graph context menu.

## Find And Replace

Open the **Replace** foldout to enable replacement mode.

### Values

Use **Values** mode to replace editable values on matching results. This can update node default values, literal values, group labels, group comments, embedded graph titles, and blackboard variable names when those items are in the result list.

Use **Selected** to replace only selected results in the list of showing nodes, or **All In Tab** to replace every replaceable result in the active tab.

### Nodes

Use **Node** mode to replace matching flow nodes with a selected node template.

1. Select a flow node in the graph.
2. Open the Replace foldout.
3. Click **Pick**.
4. Select one or more finder results.
5. Click **Selected** or **All In Tab**.

Node replacement preserves compatible connections where possible and restores the selected template defaults.

## Variable Rename

Right-click a **Get Variable**, **Set Variable**, or **Has Variable** node and choose **Rename Variable**.

The finder opens in a filtered rename mode for the clicked variable name and variable kind. This keeps scopes separate, so a Graph variable rename does not touch Object, Scene, Application, or Saved variables with the same name.

The rename flow can update:

- Get Variable nodes
- Set Variable nodes
- Has Variable nodes
- Matching blackboard variable declarations

The rename uses the active finder scopes, so use the Current Graph, All Graphs, and Hierarchy toggles to control how far the rename search reaches.

## Special Search Labels

Node Finder formats many Visual Scripting nodes with searchable labels, including:

- Variable nodes
- Custom Events
- Trigger Custom Events
- Member get, set, and invoke nodes
- Input and mouse input nodes
- Animator member nodes
- New Input System event nodes
- Literal nodes
- Time nodes with configured input values
- Graph groups
- Flow states and transitions

Useful keywords include:

- Event
- Trigger
- Variable
- Subgraph
- State
- Group
- Literal
- Time
- Delay
- Duration
- Deprecated

## User Settings

Open settings from the button beside the search field, or from:

**Preferences > Visual Scripting > UVS Finder**

## Shortcut

**Alt+F** opens Node Finder.

## Dependencies

- **com.unity.visualscripting:** 1.5.x, 1.6.x, 1.7.x, 1.8.x, 1.9.x

This package no longer depends on Harmony.

## Supported Unity Versions

- Unity 2021.1+

## Supported OS

- Windows 10
- Windows 11
- macOS (not tested)
- Linux (not tested)

## Known Issues

- https://github.com/myanko/uvs-finder/issues

## Support

This package no longer depends on Harmony.
