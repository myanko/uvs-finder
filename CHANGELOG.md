# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Can search the embedded elements in the opened scenes' hierarchy (script machines and state graphs)

### Fixed

- Better display name for the Layer Masks
- Errors with the Input methods "The given key '%axisName' was not present in the dictionary"

## [0.4.0] - 2023-05-04

### Added

- Right click on nodes contextual find options
	- Variables (Getter, Setter and Has)
	- Groups
	- Custom Events (Trigger and Events)
- Adding support to Find nodes with custom text fields, dropdown items
	- Get Button show Button Name
	- Get Axis
	- Get Button Down
	- Get Button UP
	- Animator (Work in progress)
	- GetButton
	- GetMouseButton
- New Input System Events
	- On Input System Event Float
	- On Input System Event Button
	- On Input System Event Vector 2

### Fixed

- Window partially un-dock after restoring. (Domain Reload)

## [0.3.0] - 2022-09-25

### Added

- Option to disable search in tabs with the checkbox on each tabs.
- New search filters to constraint current graph to:
	- Search in current graph.
	- Search in current graph and subgraphs.
	- Search in all graphs (Parent, current and subgraphs).
	- Search in state graphs transition nodes.

### Fixed

- SubState graph search.
- Fixed naming of some nodes.
- Fixed default highlight color black and issues with alpha.
- Fixed Can now See 3d Physic Events

## [0.2.0] - 2022-05-24

### Added

- Search in Embed ScriptMachine and StateMachine graphs from objects in scene and in project.
- Search in Hierarchy.
- Added User Defined highlight color selection in preferences.
- Search field have an erase button.
- Improvement for search information on found items.
- Type icons are now shown by default.
- Made some small UI improvements.
- Animation Event name is part of the search

### Fixed

- Node list Scrolling that shuffle items.
- Icons are no more making your scrolling lag.
- Auto focus on the search field when opening the tool.
- List items are smaller in height.
- The alignment of items (UI).
- Current graph node selection will keep the selected object graph.
- Adding missing icons for HasVariables Nodes
- Fixed Unity 2021+ Listview expanding with search...
- Special Characters breaking the search like "[ ]"
- Singles are now named Float.

### Known Issues

- Domain reload will clear current results. [https://github.com/myanko/uvs-finder/issues]

## [0.1.1] - 2022-02-23

### Fixed

- Compatibility issues with VS 1.6.x.
- Added SuperUnits search with names for VS 1.5.2.

## [0.1.0] - 2022-02-20

### Added

- Better subgraphs digging in state graphs.
- Search state graphs in state graphs.
- Script graphs now support subgraphs search.
- Replaced the deserialization to use visual scripting.
- Added support for Visual Scripting 1.8.
- Same name as in the fuzzy finder (Add is not more sum).

### Fixed

- Made the tool Editor only.
- Fixed Stylesheet warnings.
- Switched default shortcut to Alt+F to remove collision with Unity Shortcuts.
- Removed Preferences sections that are not hooked to the code.
- Operators are searchable by name.

## [0.0.4] - 2022-01-20

### Fixed

- Minimal Visual Scripting version and packages dependencies

## [0.0.3] - 2021-12-18

### Fixed

- Adding the required getaotstubs for Visual Scripting @1.7.5
- Removing a useless  Debug.Log

## [0.0.2] - 2021-12-03

### Added

- Adding search in current graph and search in all project graphs.
