namespace Lumina.Core;

/// <summary>Standard 64-color ILDA palette used when a file has no format-2 table.</summary>
public static class IldaPalette
{
    public static byte[][] Default64 { get; } = Build();

    private static byte[][] Build()
    {
        // Classic ILDA 64-color table (Pangolin / ILDA common mapping).
        int[] packed =
        [
            0x000000, 0xFF0000, 0xFF4000, 0xFF7F00, 0xFFBF00, 0xFFFF00, 0xBFFF00, 0x7FFF00,
            0x40FF00, 0x00FF00, 0x00FF40, 0x00FF7F, 0x00FFBF, 0x00FFFF, 0x00BFFF, 0x007FFF,
            0x0040FF, 0x0000FF, 0x4000FF, 0x7F00FF, 0xBF00FF, 0xFF00FF, 0xFF00BF, 0xFF007F,
            0xFF0040, 0xFF0020, 0xFFFFFF, 0xFF7F7F, 0xFFBFBF, 0xFFFFBF, 0xBFFFBF, 0x7FFF7F,
            0x7FBF7F, 0x7F7FFF, 0xBFBFFF, 0xBF7FBF, 0xBF7F7F, 0x7FBFBF, 0xBFBF7F, 0xBF7FBF,
            0x7F7F7F, 0xBFBFBF, 0xFFFFFF, 0xFF2000, 0xFF6000, 0xFFA000, 0xFFE000, 0xDFFF00,
            0x9FFF00, 0x5FFF00, 0x1FFF00, 0x00FF20, 0x00FF60, 0x00FFA0, 0x00FFE0, 0x00DFFF,
            0x009FFF, 0x005FFF, 0x001FFF, 0x2000FF, 0x6000FF, 0xA000FF, 0xE000FF, 0xFF00E0
        ];

        var table = new byte[64][];
        for (var i = 0; i < packed.Length; i++)
        {
            var c = packed[i];
            table[i] = [(byte)((c >> 16) & 0xFF), (byte)((c >> 8) & 0xFF), (byte)(c & 0xFF)];
        }

        return table;
    }
}
