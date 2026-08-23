using System.Diagnostics;

namespace Lumina.Core;

/// <summary>Master clock for playback. When MP3 or WAV is linked, audio time wins; otherwise an internal stopwatch is used.</summary>
public sealed class ShowClock
{
    private readonly Stopwatch _internal = new();
    private TimeSpan _pausedAt;
    private Func<TimeSpan>? _audioPosition;

    public bool AudioIsMaster { get; private set; }
    public bool IsRunning { get; private set; }
    public TimeSpan TimelineDuration { get; private set; } = TimeSpan.FromSeconds(5);

    public void Bind(LaserShow show, TimeSpan? audioDuration, Func<TimeSpan>? audioPosition)
    {
        var fromShow = show.TimelineDuration > TimeSpan.Zero ? show.TimelineDuration : TimeSpan.FromSeconds(5);
        AudioIsMaster = audioDuration is { TotalSeconds: > 0.25 } && audioPosition is not null;
        TimelineDuration = AudioIsMaster ? audioDuration!.Value : fromShow;
        _audioPosition = AudioIsMaster ? audioPosition : null;
    }

    public void Start()
    {
        IsRunning = true;
        if (!AudioIsMaster)
        {
            _internal.Start();
        }
    }

    public void Pause()
    {
        if (!IsRunning) return;
        IsRunning = false;
        if (!AudioIsMaster)
        {
            _pausedAt = _pausedAt + _internal.Elapsed;
            _internal.Reset();
        }
    }

    public void Stop()
    {
        IsRunning = false;
        _pausedAt = TimeSpan.Zero;
        _internal.Reset();
    }

    public TimeSpan Position
    {
        get
        {
            if (AudioIsMaster && _audioPosition is not null)
            {
                var t = _audioPosition();
                return t < TimeSpan.Zero ? TimeSpan.Zero : t;
            }

            var elapsed = _pausedAt + (_internal.IsRunning ? _internal.Elapsed : TimeSpan.Zero);
            return elapsed;
        }
    }

    public void Seek(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        if (TimelineDuration > TimeSpan.Zero && time > TimelineDuration)
        {
            time = TimelineDuration;
        }

        if (AudioIsMaster)
        {
            return;
        }

        _pausedAt = time;
        if (_internal.IsRunning)
        {
            _internal.Restart();
        }
        else
        {
            _internal.Reset();
        }
    }

    public bool Finished =>
        TimelineDuration > TimeSpan.Zero && Position >= TimelineDuration;
}
