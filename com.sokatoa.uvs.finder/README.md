# Node Finder for Unity Visual Scripting

This package adds searching functionalities to Visual Scripting, by letting you simply press Alt+F and start to browse for nodes in graphs, so you always know where your events are used in the project, or where your variables or subgraphs are used in your super huge graph.

# **Features:**

- Find nodes in current graph.

- Find nodes in all project graphs.

- Find groupes by title.

- Find custom events nodes.

- Focus on node in graph when clicking on the found item.

  

# **Dependencies**

- **com.unity.visualscripting:** 1.5.x, 1.6.x, 1.7.x


# **Supported OS:**

- Windows 10
- MacOs (Not Tested)
- Linux (Not Tested)



# Supported Unity Versions:

- 2021.1+

# **Known Issues:**

- Search in prefabs and scenes is not done.
- 1.5.x integration is not completed.
  - List items may break.
  - Not all titles are searchable as it work by types. (Ex: To find **Add** node search for **generic sum**)
- Can search only at one level of deepness in subgraphs.
- Some Icons make the list super slow. (Add operator, multiply operator,...)



# **Documentation:**



## How to Open the Tool

To open the node finder, you can simply press alt+f.



## Default ShortCuts

**Alt+F** :Open the node finder and search in all graphs in the project.



## User Settings

To open the User Settings, click on the button on the side of the search field or open the Preference panel then go to **Visual Scripting/ UVS Finder**.



## How to find nodes

After opening the node finding tool, simply type in the search field and press Enter. The finder will search



## Find special types of nodes

Some Usefull Keywords

- Event
- trigger
- Variable
- Bolt
- Subgraph
- State
- Group