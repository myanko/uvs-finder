using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.VisualScripting.UVSFinder
{
    public class UVSFinderPaths
    {
        public string findRootPackagePath()
        {
            if (File.Exists("Packages/com.sokatoa.uvs.finder/package.json"))
                return "Packages/com.sokatoa.uvs.finder/Editor";

            return AssetDatabase.GUIDToAssetPath("b3a308aa6df1fec478d83a1651665634").Replace("UVSFinderPaths.cs", "");
            //return AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(this)).Replace("UVSFinderPaths.cs", "");
        }
    }
}