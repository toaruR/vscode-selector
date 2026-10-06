namespace VscodeSelector.Tests;

internal sealed class FakeEnumerator : IWindowEnumerator
{
    public List<RawWindow> Windows { get; set; } = new();
    public bool Throw { get; set; }
    public int Calls { get; private set; }
    public IReadOnlyList<RawWindow> Enumerate()
    {
        Calls++;
        if (Throw) throw new EnumerationException(5);
        return Windows.ToList();
    }
}

internal sealed class FakeParser : ITitleParser
{
    /// <summary>タイトルをそのまま ProjectName にする。</summary>
    public ParsedTitle Parse(string title, Options options) =>
        new(title, RemoteKind.None, null, false, false);
}

internal sealed class FakeActivator : IWindowActivator
{
    public ActivationResult Result { get; set; } = ActivationResult.Success;
    public List<(nint Hwnd, uint Pid, string Class)> Calls { get; } = new();
    public Action? OnActivate { get; set; }
    public ActivationResult Activate(nint hwnd, uint expectedPid, string expectedClassName)
    {
        Calls.Add((hwnd, expectedPid, expectedClassName));
        OnActivate?.Invoke();
        return Result;
    }
    public bool MinimizeResult { get; set; } = true;
    public List<nint> MinimizeCalls { get; } = new();
    public bool TryMinimize(nint hwnd, uint expectedPid, string expectedClassName)
    {
        MinimizeCalls.Add(hwnd);
        return MinimizeResult;
    }
}

internal sealed class VirtualClock : IClock
{
    private sealed class Item(long due, Action cb) : IDisposable
    {
        public long Due = due; public Action Cb = cb; public bool Cancelled;
        public void Dispose() => Cancelled = true;
    }
    private readonly List<Item> _items = new();
    public long NowMs { get; private set; }
    public IDisposable Schedule(int delayMs, Action callback)
    {
        var i = new Item(NowMs + delayMs, callback);
        _items.Add(i);
        return i;
    }
    public void Advance(long ms)
    {
        var target = NowMs + ms;
        while (true)
        {
            var next = _items.Where(i => !i.Cancelled && i.Due <= target).OrderBy(i => i.Due).FirstOrDefault();
            if (next is null) break;
            _items.Remove(next);
            if (next.Due > NowMs) NowMs = next.Due;
            next.Cb();
        }
        NowMs = target;
    }
}
