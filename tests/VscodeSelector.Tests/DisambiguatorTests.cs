using Xunit;

namespace VscodeSelector.Tests;

public class DisambiguatorTests
{
    private static ProjectEntry E(nint h, string name, VsCodeEdition ed = VsCodeEdition.Stable,
        RemoteKind rk = RemoteKind.None, string? label = null) =>
        new(h, 1, ed, name, name, new ParsedTitle(name, rk, label, false, false), false);

    [Fact]
    public void StableTwiceAndInsiders()
    {
        var map = new Dictionary<nint, int>();
        var r = Disambiguator.Apply([E(1, "app"), E(2, "app"), E(3, "app", VsCodeEdition.Insiders)], map);
        Assert.Equal(["app", "app #2", "app (Insiders)"], r.Select(x => x.DisplayName).ToArray());
        Assert.Equal(3, r.Select(x => x.DisplayName).Distinct().Count());
    }

    [Fact]
    public void NumberingStableAcrossReenumeration()
    {
        var map = new Dictionary<nint, int>();
        Disambiguator.Apply([E(1, "app"), E(2, "app")], map);
        var r = Disambiguator.Apply([E(2, "app"), E(1, "app")], map);
        Assert.Equal("app #2", r[0].DisplayName);
        Assert.Equal("app", r[1].DisplayName);
        // reuse same map: window 1 closes, 2 keeps its number
        var r2 = Disambiguator.Apply([E(2, "app"), E(5, "app")], map);
        Assert.Equal("app #2", r2[0].DisplayName);
        Assert.Equal("app", r2[1].DisplayName);
    }

    [Fact]
    public void NoDuplicatesUnchanged()
    {
        var r = Disambiguator.Apply([E(1, "a"), E(2, "b", VsCodeEdition.Insiders)], new Dictionary<nint, int>());
        Assert.Equal(["a", "b"], r.Select(x => x.DisplayName).ToArray());
    }

    [Fact]
    public void RemoteLabelAppended()
    {
        var r = Disambiguator.Apply([E(1, "app"), E(2, "app", VsCodeEdition.Stable, RemoteKind.Wsl, "WSL: Ubuntu")], new Dictionary<nint, int>());
        Assert.Equal("app", r[0].DisplayName);
        Assert.Equal("app [WSL: Ubuntu]", r[1].DisplayName);
    }

    [Fact]
    public void EmptyInputNoThrow()
    {
        Assert.Empty(Disambiguator.Apply([], new Dictionary<nint, int>()));
    }
}
