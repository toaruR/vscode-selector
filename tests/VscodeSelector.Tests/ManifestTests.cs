using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace VscodeSelector.Tests;

public class ManifestTests
{
    private static string SrcDir()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "src", "VscodeSelector", "app.manifest")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return Path.Combine(dir!, "src", "VscodeSelector");
    }

    [Fact]
    public void Manifest_DpiAwareness_ContainsPerMonitorV2()
    {
        var doc = XDocument.Load(Path.Combine(SrcDir(), "app.manifest"));
        var el = doc.Descendants().Single(e => e.Name.LocalName == "dpiAwareness");
        Assert.Contains("PerMonitorV2", el.Value);
    }

    [Fact]
    public void Csproj_HasManifestAndWinExe()
    {
        var doc = XDocument.Load(Path.Combine(SrcDir(), "VscodeSelector.csproj"));
        Assert.Contains(doc.Descendants("ApplicationManifest"), e => e.Value == "app.manifest");
        Assert.Contains(doc.Descendants("OutputType"), e => e.Value == "WinExe");
    }

    [Fact]
    public void Manifest_IsWellFormed_WithAssemblyRoot()
    {
        var doc = XDocument.Load(Path.Combine(SrcDir(), "app.manifest"));
        Assert.Equal("assembly", doc.Root!.Name.LocalName);
        Assert.Equal("urn:schemas-microsoft-com:asm.v1", doc.Root.Name.NamespaceName);
    }

    [Fact]
    public void Csproj_TargetsNet8Windows_WithWpf()
    {
        var doc = XDocument.Load(Path.Combine(SrcDir(), "VscodeSelector.csproj"));
        Assert.Contains(doc.Descendants("TargetFramework"), e => e.Value == "net8.0-windows");
        Assert.Contains(doc.Descendants("UseWPF"), e => e.Value == "true");
    }

    [Fact]
    public void AppXaml_DeclaresApp_WithoutStartupUri_AndDefaultShutdownMode()
    {
        var doc = XDocument.Load(Path.Combine(SrcDir(), "App.xaml"));
        var root = doc.Root!;
        Assert.Equal("Application", root.Name.LocalName);
        Assert.Contains(root.Attributes(), a => a.Name.LocalName == "Class" && a.Value == "VscodeSelector.App");
        // 起動は App.OnStartup のコードで行う (StartupUri を使わない)。
        Assert.DoesNotContain(root.Attributes(), a => a.Name.LocalName == "StartupUri");
        Assert.DoesNotContain(root.Attributes(), a => a.Name.LocalName == "ShutdownMode");
    }
}
