# Node Finder for Unity Visual Scripting V0.5

This package adds searching functionalities to Visual Scripting, by letting you simply press Alt+F and start to browse for nodes in graphs, so you always know where your events are used in the project, or where your variables or subgraphs are used in your super huge graph.

# **Features:**

- Can search the embedded elements in the opened scenes' hierarchy (script machines and state graphs.

- Right click on nodes contextual find options *[NEW]*
  
  - Variables (Getter, Setter and Has)
  - Groups
  - Custom Events (Trigger and Events)

- Adding support to Find nodes with custom text fields, dropdown items *[NEW]*
  
  - Get Button show Button Name
  - Get Axis
  - Get Button Down
  - Get Button  UP
  - Get Button show Button Name
  - Get Axis
  - Animator
  - GetButton
  - GetMouseButton
  - New Input System Events
    - On Input System Event Float
    - On Input System Event Button
    - On Input System Event Vector 2

- Find nodes in current graph.

- Find nodes in all project graphs.

- Find nodes in Hierachy.

- Find groupes by title.

- Find custom events nodes.

- Focus on node in graph when clicking on the found item.

- Enable and disable search scopes.

- Search in embed graphs

- Constraint search to children and current graph in current graph tab

# **Dependencies**

- **com.unity.visualscripting:** 1.5.x, 1.6.x, 1.7.x, 1.8.x

# **Supported OS:**

- Windows 10
- Windows 11
- MacOs (Not Tested)
- Linux (Not Tested)

# Supported Unity Versions:

- 2021.1+

# **Known Issues:**

- https://github.com/myanko/uvs-finder/issues

# **Documentation:**

## How to Open the Tool

To open the node finder, you can simply press Alt+F.

## Default ShortCuts

**Alt+F** :Open the node finder and search in all graphs in the project.

## User Settings

To open the User Settings, click on the button on the side of the search field or open the Preference panel then go to **Visual Scripting/ UVS Finder**.

### 

## How to find nodes

After opening the node finding tool, simply type in the search field and press Enter. The finder will search

You can also right click on nodes in a graph, to get contextual search options.

## How to Update

Delete the folder com.sokatoa.uvs.finder and import the node finder from the asset store.

## Find special types of nodes

Some Usefull Keywords

- Event
- Trigger
- Variable
- Bolt
- Subgraph
- State
- Group
- Deprecated

# Support US

- [Buy Sokatoa a Coffee](https://ko-fi.com/sokatoa)

- https://patreon.com/myanko

#Opensource Code
**Harmony** is used as a solution to add right click options in different context. You can find the License to **Harmony**
Copyright (c) 2017 Andreas Pardeike
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.