using System.Windows.Threading;
using NAudio.Wave;

namespace Lumina.App;

/// <summary>Playback for MP3 and WAV (and other NAudio formats), driving the show clock.</summary>
public sealed class ShowAudioPlayer : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private AudioFileReader? _reader;
    private WaveOutEvent? _output;
    private bool _stopping;

    public ShowAudioPlayer(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public event Action? Opened;
    public event Action? Ended;
    public event Action<string>? Failed;

    public bool IsOpen => _reader is not null;
    public bool HasDuration => _reader is { TotalTime.TotalSeconds: > 0.05 };
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get
        {
            try { return _reader?.CurrentTime ?? TimeSpan.Zero; }
            catch { return TimeSpan.Zero; }
        }
        set
        {
            if (_reader is null) return;
            var t = value;
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t > _reader.TotalTime) t = _reader.TotalTime;
            try { _reader.CurrentTime = t; }
            catch { /* some codecs reject a seek while stopped */ }
        }
    }

    public double Volume
    {
        get => _reader?.Volume ?? 1;
        set
        {
            if (_reader is not null)
            {
                _reader.Volume = (float)Math.Clamp(value, 0, 1);
            }
        }
    }

    public void Open(string path)
    {
        Close();
        try
        {
            _reader = new AudioFileReader(path);
            _output = new WaveOutEvent { DesiredLatency = 80 };
            _output.Init(_reader);
            _output.PlaybackStopped += OnStopped;
            Opened?.Invoke();
        }
        catch (Exception ex)
        {
            Close();
            Failed?.Invoke(ex.Message);
        }
    }

    public void Play() => _output?.Play();

    public void Pause() => _output?.Pause();

    public void Stop()
    {
        _stopping = true;
        try { _output?.Stop(); }
        catch { /* already stopped */ }
        Position = TimeSpan.Zero;
        _stopping = false;
    }

    public void Close()
    {
        _stopping = true;
        if (_output is not null)
        {
            _output.PlaybackStopped -= OnStopped;
            try { _output.Stop(); } catch { }
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
        _stopping = false;
    }

    public void Dispose() => Close();

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        void Raise()
        {
            if (e.Exception is not null)
            {
                Failed?.Invoke(e.Exception.Message);
                return;
            }

            if (_stopping || _reader is null) return;
            if (_reader.CurrentTime >= _reader.TotalTime - TimeSpan.FromMilliseconds(120))
            {
                Ended?.Invoke();
            }
        }

        if (_dispatcher.CheckAccess()) Raise();
        else _dispatcher.BeginInvoke(Raise);
    }
}
