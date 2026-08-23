using NAudio.Wave;
using Lumina.Core;

namespace Lumina.App;

public static class BeatAnalyzer
{
    public static BeatGrid Analyze(string path)
    {
        using var reader = new AudioFileReader(path);
        var sampleRate = Math.Max(1, reader.WaveFormat.SampleRate);
        var channels = Math.Max(1, reader.WaveFormat.Channels);
        const int hop = 1024;
        var energy = new List<float>(4096);
        var buffer = new float[hop * channels];
        int read;
        while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            double sum = 0;
            var n = 0;
            for (var i = 0; i < read; i += channels)
            {
                float mix = 0;
                for (var c = 0; c < channels && i + c < read; c++)
                {
                    mix += buffer[i + c];
                }

                mix /= channels;
                sum += mix * mix;
                n++;
            }

            energy.Add(n == 0 ? 0 : (float)Math.Sqrt(sum / n));
        }

        var hopSeconds = hop / (double)sampleRate;
        return BeatGrid.FromOnsetEnergy(energy, hopSeconds);
    }
}
