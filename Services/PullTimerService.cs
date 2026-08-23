using System.Diagnostics;

namespace RaidFlow.Services;

public sealed class PullTimerService
{
    private readonly Stopwatch stopwatch = new();
    private float baseSeconds;

    public bool IsRunning => this.stopwatch.IsRunning;

    public float CurrentTimeSeconds => this.baseSeconds + (float)this.stopwatch.Elapsed.TotalSeconds;

    public void StartFrom(float seconds = 0)
    {
        this.baseSeconds = Math.Max(0, seconds);
        this.stopwatch.Restart();
    }

    public void Pause()
    {
        if (!this.stopwatch.IsRunning)
        {
            return;
        }

        this.baseSeconds = this.CurrentTimeSeconds;
        this.stopwatch.Reset();
    }

    public void Resume()
    {
        if (this.stopwatch.IsRunning)
        {
            return;
        }

        this.stopwatch.Restart();
    }

    public void Reset()
    {
        this.baseSeconds = 0;
        this.stopwatch.Reset();
    }

    public void SetCurrentTime(float seconds)
    {
        this.baseSeconds = Math.Max(0, seconds);
        if (this.stopwatch.IsRunning)
        {
            this.stopwatch.Restart();
        }
        else
        {
            this.stopwatch.Reset();
        }
    }

    public void Nudge(float seconds)
    {
        this.SetCurrentTime(this.CurrentTimeSeconds + seconds);
    }
}
