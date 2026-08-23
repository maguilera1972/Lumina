using NAudio.Wave;

namespace Lumina.App;

public static class WaveformSampler
{
    public static float[] Sample(string path, int buckets)
    {
        if (buckets < 8) buckets = 8;
        using var reader = new AudioFileReader(path);
        var channels = Math.Max(1, reader.WaveFormat.Channels);
        var buffer = new float[channels * 4096];
        var peaks = new float[buckets];
        var total = reader.Length / sizeof(float);
        if (total <= 0) return peaks;
        var perBucket = Math.Max(1.0, (double)total / buckets);
        double consumed = 0;
        var bucket = 0;
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0 && bucket < buckets)
        {
            for (var i = 0; i < read; i++)
            {
                var a = Math.Abs(buffer[i]);
                if (a > peaks[bucket]) peaks[bucket] = a;
                consumed++;
                if (consumed >= perBucket)
                {
                    consumed = 0;
                    bucket++;
                    if (bucket >= buckets) break;
                }
            }
        }

        var max = peaks.Max();
        if (max > 0)
        {
            for (var i = 0; i < peaks.Length; i++) peaks[i] /= max;
        }

        return peaks;
    }
}
