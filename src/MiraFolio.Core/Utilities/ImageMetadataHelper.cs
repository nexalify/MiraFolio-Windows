namespace MiraFolio.Core.Utilities;

public static class ImageMetadataHelper
{
    public static bool TryReadDimensions(string filePath, out int width, out int height)
    {
        width = 0;
        height = 0;

        if (!File.Exists(filePath)) return false;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var header = new byte[24];
        int bytesRead = fs.Read(header, 0, header.Length);
        if (bytesRead < 4) return false;

        // PNG
        if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
        {
            if (bytesRead < 24) return false;
            width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            return width > 0 && height > 0;
        }

        // JPEG
        if (header[0] == 0xFF && header[1] == 0xD8)
            return TryReadJpegDimensions(fs, out width, out height);

        // BMP
        if (header[0] == 0x42 && header[1] == 0x4D && bytesRead >= 22)
        {
            width = header[18] | (header[19] << 8) | (header[20] << 16) | (header[21] << 24);
            var bmpExtra = new byte[4];
            fs.Seek(22, SeekOrigin.Begin);
            if (fs.Length - fs.Position < bmpExtra.Length) return false;
            fs.ReadExactly(bmpExtra);
            height = Math.Abs(bmpExtra[0] | (bmpExtra[1] << 8) | (bmpExtra[2] << 16) | (bmpExtra[3] << 24));
            return width > 0 && height > 0;
        }

        // WebP
        if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
            return TryReadWebpDimensions(fs, out width, out height);

        return false;
    }

    private static bool TryReadJpegDimensions(FileStream fs, out int width, out int height)
    {
        width = 0;
        height = 0;

        fs.Seek(2, SeekOrigin.Begin);
        var buf = new byte[4];
        while (fs.Position < fs.Length - 4)
        {
            if (fs.Read(buf, 0, 2) < 2) break;
            if (buf[0] != 0xFF) break;

            byte marker = buf[1];
            if (fs.Read(buf, 0, 2) < 2) break;
            int length = (buf[0] << 8) | buf[1];

            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                var sof = new byte[5];
                if (fs.Read(sof, 0, 5) < 5) break;
                width = (sof[3] << 8) | sof[4];
                height = (sof[1] << 8) | sof[2];
                return width > 0 && height > 0;
            }

            fs.Seek(length - 2, SeekOrigin.Current);
        }

        return false;
    }

    private static bool TryReadWebpDimensions(FileStream fs, out int width, out int height)
    {
        width = 0;
        height = 0;

        var buf = new byte[30];
        fs.Seek(0, SeekOrigin.Begin);
        if (fs.Read(buf, 0, 30) < 30) return false;

        if (buf[12] == 'V' && buf[13] == 'P' && buf[14] == '8' && buf[15] == ' ')
        {
            width = (buf[26] | (buf[27] << 8)) & 0x3FFF;
            height = (buf[28] | (buf[29] << 8)) & 0x3FFF;
            return width > 0 && height > 0;
        }

        if (buf[12] == 'V' && buf[13] == 'P' && buf[14] == '8' && buf[15] == 'L')
        {
            uint bits = (uint)(buf[21] | (buf[22] << 8) | (buf[23] << 16) | (buf[24] << 24));
            width = (int)(bits & 0x3FFF) + 1;
            height = (int)((bits >> 14) & 0x3FFF) + 1;
            return width > 0 && height > 0;
        }

        if (buf[12] == 'V' && buf[13] == 'P' && buf[14] == '8' && buf[15] == 'X')
        {
            width = 1 + buf[24] + (buf[25] << 8) + (buf[26] << 16);
            height = 1 + buf[27] + (buf[28] << 8) + (buf[29] << 16);
            return width > 0 && height > 0;
        }

        return false;
    }
}
