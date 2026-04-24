using System;
using System.Linq;
using NUnit.Framework;

namespace Unity.VisualScripting.UVSFinder.Tests
{
    public class UVSGraphElementWidgetExt
    {
        [Test]
        public void GetDropdownOptions_ForUnitWithScript_IncludesOpenCSharpScript()
        {
            var options = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.GetDropdownOptions(new WaitForSecondsUnit()).ToArray();

            Assert.That(options.Any(option => option.label == "Open C# Script" && option.value is Action), Is.True);
        }

        [Test]
        public void GetDropdownOptions_ForNonUnit_DoesNotIncludeOpenCSharpScript()
        {
            var options = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.GetDropdownOptions(new GraphGroup()).ToArray();

            Assert.That(options.Any(option => option.label == "Open C# Script"), Is.False);
        }
    }
}
