namespace Lumina.Core;

/// <summary>Beat grid estimated from audio, used to snap laser clips to the music.</summary>
public sealed class BeatGrid
{
    public double Bpm { get; init; } = 120;
    public TimeSpan FirstBeat { get; init; }
    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan BeatPeriod =>
        TimeSpan.FromMinutes(1.0 / Math.Clamp(Bpm, 40, 240));

    public int BeatCount
    {
        get
        {
            var p = BeatPeriod.TotalSeconds;
            if (p <= 0) return 0;
            return Math.Max(1, (int)Math.Round(Duration.TotalSeconds / p));
        }
    }

    public TimeSpan Snap(TimeSpan time)
    {
        var period = BeatPeriod.TotalSeconds;
        if (period <= 0) return time < TimeSpan.Zero ? TimeSpan.Zero : time;
        var origin = FirstBeat.TotalSeconds;
        var n = Math.Round((time.TotalSeconds - origin) / period);
        var snapped = origin + n * period;
        if (snapped < 0) snapped = 0;
        return TimeSpan.FromSeconds(snapped);
    }

    public IReadOnlyList<TimeSpan> Beats()
    {
        var period = BeatPeriod;
        if (period <= TimeSpan.Zero || Duration <= TimeSpan.Zero) return [];
        var t = FirstBeat;
        while (t > TimeSpan.Zero) t -= period;
        if (t < TimeSpan.Zero) t += period;
        var list = new List<TimeSpan>();
        while (t <= Duration + TimeSpan.FromMilliseconds(1))
        {
            list.Add(t);
            t += period;
        }

        return list;
    }

    /// <summary>Estimate BPM and first downbeat from per-hop RMS energy.</summary>
    public static BeatGrid FromOnsetEnergy(IReadOnlyList<float> energy, double hopSeconds)
    {
        var duration = TimeSpan.FromSeconds(Math.Max(hopSeconds, energy.Count * hopSeconds));
        if (energy.Count < 16 || hopSeconds <= 0)
        {
            return new BeatGrid { Bpm = 120, FirstBeat = TimeSpan.Zero, Duration = duration };
        }

        var onset = new float[energy.Count];
        for (var i = 1; i < energy.Count; i++)
        {
            onset[i] = Math.Max(0, energy[i] - energy[i - 1]);
        }

        var lagMin = Math.Max(2, (int)Math.Round(60.0 / 180.0 / hopSeconds));
        var lagMax = Math.Min(energy.Count / 3, (int)Math.Round(60.0 / 70.0 / hopSeconds));
        if (lagMax <= lagMin)
        {
            return new BeatGrid { Bpm = 120, FirstBeat = TimeSpan.Zero, Duration = duration };
        }

        var bestLag = lagMin;
        var best = double.MinValue;
        for (var lag = lagMin; lag <= lagMax; lag++)
        {
            double sum = 0;
            var n = 0;
            for (var i = 0; i + lag < onset.Length; i++)
            {
                sum += onset[i] * onset[i + lag];
                n++;
            }

            if (n == 0) continue;
            var score = sum / n;
            if (score > best)
            {
                best = score;
                bestLag = lag;
            }
        }

        var bpm = Math.Clamp(60.0 / (bestLag * hopSeconds), 70, 180);
        var window = Math.Max(1, Math.Min(onset.Length, (int)(2.5 / hopSeconds)));
        var maxI = 0;
        var maxV = onset[0];
        for (var i = 1; i < window; i++)
        {
            if (onset[i] <= maxV) continue;
            maxV = onset[i];
            maxI = i;
        }

        return new BeatGrid
        {
            Bpm = bpm,
            FirstBeat = TimeSpan.FromSeconds(maxI * hopSeconds),
            Duration = duration
        };
    }
}
