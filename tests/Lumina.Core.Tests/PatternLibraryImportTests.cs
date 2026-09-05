using Lumina.Core;

namespace Lumina.Core.Tests;

public class PatternLibraryImportTests
{
    [Fact]
    public void ImportIlda_FraExtension_UsesFraReaderNotIldaHeader()
    {
        var dir = Directory.CreateTempSubdirectory("lumina-fra-import");
        try
        {
            var path = Path.Combine(dir.FullName, "Trance and Acid.fra");
            // Minimal MediaLas-ish payload: no ILDA "ILDA" header — must not throw IldaReader error.
            File.WriteAllBytes(path, new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x64, 0x00, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                0x10, 0x00, 0x20, 0x00, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x80
            });

            var lib = new PatternLibrary();
            var item = lib.ImportIlda(path);

            Assert.Equal("Trance and Acid", item.Name);
            Assert.Single(lib.Items);
            Assert.True(item.Frames.Count >= 1);
            Assert.Equal(-1, item.Frames[0].Format);
        }
        finally
        {
            try { dir.Delete(recursive: true); } catch { /* ignore */ }
        }
    }
}
