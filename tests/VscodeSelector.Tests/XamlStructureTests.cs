using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace VscodeSelector.Tests;

public class XamlStructureTests
{
    private static readonly XNamespace P = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static XDocument Load()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "src", "VscodeSelector", "MainWindow.xaml")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!, "src", "VscodeSelector", "MainWindow.xaml"));
    }

    private static IEnumerable<XElement> Els(string name) => Load().Descendants(P + name);

    [Fact]
    public void WrapPanel_Single_And_ScrollBarHidden()
    {
        Assert.Single(Els("WrapPanel"));
        Assert.Contains(Els("ScrollViewer"), s => (string?)s.Attribute("VerticalScrollBarVisibility") == "Hidden");
    }

    [Fact]
    public void ButtonTemplate_ToolTipAndAutomationName_BoundToFullTitle()
    {
        var btn = Els("DataTemplate").Descendants(P + "Button").Single();
        Assert.Equal("{Binding FullTitle}", (string?)btn.Attribute("ToolTip"));
        Assert.Equal("{Binding FullTitle}", (string?)btn.Attribute("AutomationProperties.Name"));
    }

    [Fact]
    public void InputBindings_Ctrl1to9_Enter_Esc()
    {
        var kbs = Els("Window.InputBindings").Elements(P + "KeyBinding").ToList();
        for (int i = 1; i <= 9; i++)
            Assert.Contains(kbs, k => (string?)k.Attribute("Key") == "D" + i
                && (string?)k.Attribute("Modifiers") == "Control");
        Assert.Equal(9, kbs.Count(k => (string?)k.Attribute("Modifiers") == "Control"));
        Assert.Contains(kbs, k => (string?)k.Attribute("Key") == "Enter");
        Assert.Contains(kbs, k => (string?)k.Attribute("Key") == "Esc");
    }

    [Fact]
    public void EmptyText_And_GripBar24()
    {
        Assert.Contains(Els("TextBlock"), t => (string?)t.Attribute("Text") == "開いている VS Code がありません");
        Assert.Contains(Els("Border"), e => (string?)e.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml")) == "GripBar"
            && (string?)e.Attribute("Width") == "24");
        Assert.Equal(24, Constants.StatusBarHeight);
    }

    [Fact]
    public void Button_Dimensions_150x48_Margin6()
    {
        var btn = Els("DataTemplate").Descendants(P + "Button").Single();
        Assert.Equal("150", (string?)btn.Attribute("Width"));
        Assert.Equal("48", (string?)btn.Attribute("Height"));
        Assert.Equal("6", (string?)btn.Attribute("Margin"));
        Assert.Equal(150, Constants.ButtonWidth);
        Assert.Equal(48, Constants.ButtonHeight);
        Assert.Equal(6, Constants.ButtonMargin);
    }
}
