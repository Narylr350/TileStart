using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;

namespace TileStart.Host.Utilities;

internal sealed class RenderFrameProbe : IDisposable
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("TILESTART_PROFILE_RENDER") == "1";

    private readonly string _scenario;
    private readonly List<double> _frameIntervals = [];
    private readonly DispatcherTimer _stopTimer;
    private readonly TimeSpan _duration;
    private readonly long _startedTimestamp;
    private long _lastTimestamp;
    private bool _disposed;

    private RenderFrameProbe(Dispatcher dispatcher, string scenario, TimeSpan duration)
    {
        _scenario = scenario;
        _duration = duration;
        _startedTimestamp = Stopwatch.GetTimestamp();
        _stopTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle, dispatcher)
        {
            Interval = duration,
        };
        _stopTimer.Tick += StopTimerTick;
        CompositionTarget.Rendering += CompositionTargetRendering;
        _stopTimer.Start();
    }

    public static RenderFrameProbe? Start(Dispatcher dispatcher, string scenario, TimeSpan duration) =>
        Enabled ? new RenderFrameProbe(dispatcher, scenario, duration) : null;

    private void CompositionTargetRendering(object? sender, EventArgs e)
    {
        var timestamp = Stopwatch.GetTimestamp();
        if (!IsWithinDuration(Stopwatch.GetElapsedTime(_startedTimestamp, timestamp), _duration))
        {
            Dispose();
            return;
        }

        if (_lastTimestamp != 0)
        {
            _frameIntervals.Add(Stopwatch.GetElapsedTime(_lastTimestamp, timestamp).TotalMilliseconds);
        }

        _lastTimestamp = timestamp;
    }

    private void StopTimerTick(object? sender, EventArgs e) => Dispose();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopTimer.Stop();
        _stopTimer.Tick -= StopTimerTick;
        CompositionTarget.Rendering -= CompositionTargetRendering;
        if (_frameIntervals.Count == 0)
        {
            DiagnosticLog.Write($"Render probe: scenario={_scenario}, frames=0.");
            return;
        }

        var longestIntervals = _frameIntervals
            .Select((interval, index) => new { Frame = index + 1, Interval = interval })
            .OrderByDescending(item => item.Interval)
            .Take(5)
            .Select(item => $"{item.Frame}:{item.Interval:F2}")
            .ToArray();
        _frameIntervals.Sort();
        var average = _frameIntervals.Average();
        var percentile95 = Percentile(_frameIntervals, 0.95);
        var maximum = _frameIntervals[^1];
        var over16Milliseconds = _frameIntervals.Count(interval => interval > 16.7);
        var over33Milliseconds = _frameIntervals.Count(interval => interval > 33.3);
        DiagnosticLog.Write(
            $"Render probe: scenario={_scenario}, frames={_frameIntervals.Count + 1}, " +
            $"averageMs={average:F2}, p95Ms={percentile95:F2}, maxMs={maximum:F2}, " +
            $"over16_7={over16Milliseconds}, over33_3={over33Milliseconds}, " +
            $"longest={string.Join(',', longestIntervals)}.");
    }

    internal static bool IsWithinDuration(TimeSpan elapsed, TimeSpan duration) => elapsed <= duration;

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }
}
