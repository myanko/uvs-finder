using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using System;


namespace Unity.VisualScripting.UVSFinder.Tests {

    public class UVSGraphElementWidgetExt : MonoBehaviour
    {
        /*[Test]
        [TestCase("Debug Log")]
        public void RightClickNode_Displays_Options(string name)
        {
            Utilities.OpenVisualScriptAsset("Assets/TestAsset/furetscriptgraph1.asset");
            var element = GetElementByName(name, GraphWindow.activeContext.canvas.graph.elements);
            var instance = GraphWindow.activeContext.canvas.Widget(element);
            // TODO: I am stuck here because the instance is not what is expected... 
            var actual = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.Postfix1(null, instance);
            DropdownOption[] expected = { new DropdownOption("Find") };

            var @enum = actual.GetEnumerator();

            Assert.IsTrue(@enum.MoveNext());
            Assert.AreEqual(@enum.Current.label, "Find");
            Assert.IsTrue(@enum.MoveNext());
            Assert.AreEqual(@enum.Current.label, "Find");
        }

        private IGraphElement GetElementByName(string name, MergedGraphElementCollection elements)
        {
            foreach (var e in elements)
            {
                if (GraphElement.GetElementName(e) == name)
                {
                    return e;
                }
            }

            return null;
        }*/
    }
}
