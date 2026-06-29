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

        [Test]
        public void GetDropdownOptions_ForVariableUnit_IncludesRenameVariable()
        {
            var unit = new GetVariable
            {
                kind = VariableKind.Graph
            };
            unit.EnsureDefined();
            unit.defaultValues["name"] = "score";

            var options = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.GetDropdownOptions(unit).ToArray();

            Assert.That(options.Any(option => option.label == "Rename Variable \"score\"..." && option.value is Action), Is.True);
        }

        [Test]
        public void GetVariableDeclarationDropdownOptions_ForBlackboardVariable_IncludesRenameVariable()
        {
            var declaration = new VariableDeclaration("score", 0);

            var options = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.GetVariableDeclarationDropdownOptions(declaration, VariableKind.Graph).ToArray();

            Assert.That(options.Any(option => option.label == "Rename Variable \"score\"..." && option.value is Action), Is.True);
        }

        [Test]
        public void GetDropdownOptions_ForCustomEventUnit_IncludesRenameEvent()
        {
            var unit = new CustomEvent();
            unit.EnsureDefined();
            unit.defaultValues["name"] = "Wave";

            var options = Unity.VisualScripting.UVSFinder.UVSGraphElementWidgetExt.GetDropdownOptions(unit).ToArray();

            Assert.That(options.Any(option => option.label == "Rename Event \"Wave\"..." && option.value is Action), Is.True);
        }

        [Test]
        public void GetElementName_ForWaitForSeconds_IncludesInputValues()
        {
            var unit = new WaitForSecondsUnit();
            unit.EnsureDefined();
            unit.defaultValues["seconds"] = 2.5f;
            unit.defaultValues["unscaledTime"] = true;

            var name = GraphElement.GetElementName(unit);

            Assert.That(name, Is.EqualTo("Wait For Seconds [Delay: 2.5, Unscaled: True]"));
        }

        [Test]
        public void GetElementName_ForTimer_IncludesInputValues()
        {
            var unit = new Timer();
            unit.EnsureDefined();
            unit.defaultValues["duration"] = 4f;
            unit.defaultValues["unscaledTime"] = true;

            var name = GraphElement.GetElementName(unit);

            Assert.That(name, Is.EqualTo("Timer [Duration: 4, Unscaled: True]"));
        }

        [Test]
        public void GetElementName_ForWaitForFlow_IncludesHeaderValues()
        {
            var unit = new WaitForFlow
            {
                inputCount = 4,
                resetOnExit = true
            };

            var name = GraphElement.GetElementName(unit);

            Assert.That(name, Is.EqualTo("Wait For Flow [Inputs: 4, Reset On Exit: True]"));
        }

        [Test]
        public void GetElementSearchTerms_ForUnitHeaderInspectable_IncludesHeaderValue()
        {
            var unit = new Sequence
            {
                outputCount = 4
            };

            var terms = GraphElement.GetElementSearchTerms(unit).ToArray();

            Assert.That(terms, Does.Contain("Steps: 4"));
            Assert.That(terms, Does.Contain("outputCount: 4"));
        }
    }
}
