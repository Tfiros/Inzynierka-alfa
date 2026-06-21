namespace ItemTradeApp.Features.Shared.Images;

public static class ImageExtensionValidator
{
    private static readonly Dictionary<string, ImageSignature> Signatures = new()
    {
        [".png"] = new ImageSignature(
            "image/png",
            [
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            ]),

        [".jpg"] = new ImageSignature(
            "image/jpeg",
            [
                new byte[] { 0xFF, 0xD8, 0xFF }
            ]),

        [".jpeg"] = new ImageSignature(
            "image/jpeg",
            [
                new byte[] { 0xFF, 0xD8, 0xFF }
            ]),

        [".webp"] = new ImageSignature(
            "image/webp",
            [
                new byte[] { 0x52, 0x49, 0x46, 0x46 }
            ])
    };

    public static bool IsImageValid(
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

        if (!Signatures.TryGetValue(extension, out var expected))
            return false;

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !string.Equals(file.ContentType, expected.ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Span<byte> header = stackalloc byte[12];

        using var stream = file.OpenReadStream();

        var read = stream.Read(header);

        if (read < expected.MinHeaderLength)
            return false;

        var hasValidSignature = false;

        foreach (var signature in expected.Signatures)
        {
            if (header[..signature.Length].SequenceEqual(signature))
            {
                hasValidSignature = true;
                break;
            }
        }

        if (!hasValidSignature)
            return false;

        if (extension == ".webp" && !IsWebp(header, read))
            return false;

        contentType = expected.ContentType;

        return true;
    }

    private static bool IsWebp(
        Span<byte> header,
        int read)
    {
        if (read < 12)
            return false;

        return header[8] == 0x57 &&
               header[9] == 0x45 &&
               header[10] == 0x42 &&
               header[11] == 0x50;
    }

    private sealed record ImageSignature(
        string ContentType,
        List<byte[]> Signatures)
    {
        public int MinHeaderLength =>
            Signatures.Max(signature => signature.Length);
    }
}