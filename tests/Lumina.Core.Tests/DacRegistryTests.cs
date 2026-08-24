using Lumina.Core;
using Lumina.Dac;
using Xunit;

namespace Lumina.Core.Tests;

public class DacRegistryTests
{
    [Theory]
    [InlineData(DacDriverKind.Preview, typeof(PreviewDac))]
    [InlineData(DacDriverKind.MediaLasUsbBox, typeof(UsbBoxDac))]
    [InlineData(DacDriverKind.Fb3Usb, typeof(Fb3Dac))]
    [InlineData(DacDriverKind.Fb4Network, typeof(Fb4Dac))]
    [InlineData(DacDriverKind.OpenDacBridge, typeof(OpenDacBridgeDac))]
    public void Registry_creates_expected_driver(DacDriverKind kind, Type expected)
    {
        var dac = DacRegistry.Create(kind);
        Assert.IsType(expected, dac);
        Assert.Equal(kind, dac.DriverKind);
    }

    [Fact]
    public void Driver_labels_cover_new_backends()
    {
        Assert.Contains("FB3", DacColorCompatibility.DriverLabel(DacDriverKind.Fb3Usb));
        Assert.Contains("FB4", DacColorCompatibility.DriverLabel(DacDriverKind.Fb4Network));
        Assert.Contains("DAC", DacColorCompatibility.DriverLabel(DacDriverKind.OpenDacBridge));
    }
}
