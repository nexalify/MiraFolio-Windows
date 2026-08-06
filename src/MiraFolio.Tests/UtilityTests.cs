using MiraFolio.Core.Models;
using MiraFolio.Core.Utilities;
using Xunit;

namespace MiraFolio.Tests;

public class UtilityTests
{
    [Fact]
    public void WallpaperInterval_HugeValue_ClampsToTimerLimit()
    {
        var assignment = new WallpaperAssignment
        {
            IntervalValue = int.MaxValue,
            IntervalUnit = WallpaperIntervalUnit.Hours
        };

        var interval = WallpaperIntervalHelper.ToTimeSpan(assignment, fallbackMinutes: 30);

        Assert.Equal(TimeSpan.FromDays(49), interval);
    }

    [Fact]
    public void ImageMetadata_Vp8ExtendedHeader_ReadsDimensions()
    {
        var path = Path.Combine(Path.GetTempPath(), $"MiraFolio_VP8X_{Guid.NewGuid():N}.webp");
        try
        {
            const int expectedWidth = 2560;
            const int expectedHeight = 1440;
            var bytes = new byte[30];
            bytes[0] = (byte)'R';
            bytes[1] = (byte)'I';
            bytes[2] = (byte)'F';
            bytes[3] = (byte)'F';
            bytes[8] = (byte)'W';
            bytes[9] = (byte)'E';
            bytes[10] = (byte)'B';
            bytes[11] = (byte)'P';
            bytes[12] = (byte)'V';
            bytes[13] = (byte)'P';
            bytes[14] = (byte)'8';
            bytes[15] = (byte)'X';
            WriteUInt24LittleEndian(bytes, 24, expectedWidth - 1);
            WriteUInt24LittleEndian(bytes, 27, expectedHeight - 1);
            File.WriteAllBytes(path, bytes);

            var success = ImageMetadataHelper.TryReadDimensions(path, out var width, out var height);

            Assert.True(success);
            Assert.Equal(expectedWidth, width);
            Assert.Equal(expectedHeight, height);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void WriteUInt24LittleEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
    }
}
