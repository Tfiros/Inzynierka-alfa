namespace ItemTradeApp.Features.Shared.Images;

public static class ImageExtensionValidator
{
    private static readonly Dictionary<string, byte[]> FileSignatures = new()
    {
        [".png"] = [0x89, 0x50, 0x4E, 0x47],
        [".jpg"] = [0xFF, 0xD8, 0xFF],
        [".jpeg"] = [0xFF, 0xD8, 0xFF],
        [".webp"] = [0x52, 0x49, 0x46, 0x46]
    };

    private static readonly Dictionary<string, string> ContentTypes = new()
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    public static bool IsValidImage(
        IFormFile file,
        out string extension,
        out string contentType)
    {
        extension = string.Empty;
        contentType = string.Empty;

        if (file is null || file.Length == 0)
            return false;

        extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!FileSignatures.TryGetValue(extension, out var expectedSignature))
            return false;

        if (!ContentTypes.TryGetValue(extension, out var expectedContentType))
            return false;

        if (!string.Equals(
                file.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var header = new byte[12];

        using var stream = file.OpenReadStream();

        var read = stream.Read(header, 0, header.Length);

        if (read < expectedSignature.Length)
            return false;

        var signatureMatches = header
            .Take(expectedSignature.Length)
            .SequenceEqual(expectedSignature);

        if (!signatureMatches)
            return false;

        if (extension == ".webp" && !IsWebp(header, read))
            return false;

        contentType = expectedContentType;

        return true;
    }

    private static bool IsWebp(
        byte[] header,
        int read)
    {
        if (read < 12)
            return false;

        return header[8] == 0x57 &&
               header[9] == 0x45 &&
               header[10] == 0x42 &&
               header[11] == 0x50;
    }
}