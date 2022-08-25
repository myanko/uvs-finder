# Editor Patches
Uses harmony to patch the Unity Editor.    
Settings and a list of currently active patches can be found under ``"Edit/Project Settings/Needle/Editor Patch Manager"``

## Features
- Allow outputting harmony logs to Unity console
- Set harmony log file path to ``<current Unity project>/Logs``
- Allow to disable / enable individual patches for debugging (see **Best practices** section below)

## Built-in patches
**Harmony Log To Console** - Enable to print harmony log messages to Unity console  
**Harmony Instance Registry** - Patches the harmony constructor to find/register all harmony patches. Allows to enable/disable individual patches in the editor (only works if this patch is applied *BEFORE* any patches are created).

## Best practices
When using this package to patch editor methods it is recommended to enable your patches from within ``[InitializeOnLoadMethod]`` instead of ``[InitializeOnLoad]`` to give the EditorPatchManager a chance to enable the harmony constructor patch. That way individual patches can enabled or disabled for debugging. 

## Patching
In general all patches can be applied as described in [Harmony docs](https://harmony.pardeike.net/).  
This package comes with some helper classes that though *can* be used for Editor-patching only. 

### EditorPatchProvider
Helper class to feed editor patches to PatchManager. To make it work implement ``OnGetPatches`` and add your ``EditorPatch`` instances to the list.
```csharp
public class MyPatchProvider : EditorPatchProvider
{
    protected override void OnGetPatches(List<EditorPatch> patches)
    {
        patches.Add(new MyPatch());
    }
}
```
### EditorPatch
Helper class to implement patches. Implement ``OnGetTargetMethods`` and add the methods this patch should be applied to to the provided list.
```csharp
private class MyPatch : EditorPatch
{
    protected override Task OnGetTargetMethods(List<MethodBase> targetMethods)
    {
        // add methods to target methods here
        return Task.CompletedTask;
    }

    // you can implement the same patch methods 
    // as described in the harmony documentation
    // https://harmony.pardeike.net/articles/patching.html
    // e.g. Prefix, Postfix, Transpiler, Finalizer
    // Reverse patching is untested / not implemented ?

    private static bool Prefix(object __instance)
    {
        return true;
    }

    private static void Postfix()
    {
    }
}
```

### PatchMeta : Attribute
Use this attribute to provide meta information to the PatchManager UI. Add it to classes marked with ``HarmonyPatch`` or ``EditorPatchProvider``.  
Be aware that the attribute is editor only!


## Contact ✒️
<b>[🌵 needle — tools for unity](https://needle.tools)</b> • 
[@NeedleTools](https://twitter.com/NeedleTools) • 
[@marcel_wiessler](https://twitter.com/marcel_wiessler) • 
[@hybridherbst](https://twitter.com/hybridherbst)

**Powered by** https://github.com/pardeike/Harmony
