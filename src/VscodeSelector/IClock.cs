namespace VscodeSelector;

/// <summary>時刻とタイマーの抽象 (仮想時計でスケジューラを検証するため)。</summary>
public interface IClock
{
    /// <summary>単調増加のミリ秒。</summary>
    long NowMs { get; }

    /// <summary>delayMs 後に callback を 1 回呼ぶ。Dispose で取り消す。</summary>
    IDisposable Schedule(int delayMs, Action callback);
}

public sealed class SystemClock : IClock
{
    public long NowMs => Environment.TickCount64;

    public IDisposable Schedule(int delayMs, Action callback)
    {
        var timer = new Timer(_ => callback(), null, Math.Max(0, delayMs), Timeout.Infinite);
        return timer;
    }
}
