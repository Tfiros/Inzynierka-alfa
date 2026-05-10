namespace ItemTradeApp.Features.Images;

public static class ImageExtensionValidator
{
    private static readonly Dictionary<string, List<byte[]>> Signatures = new()
    {
        [".png"] =
        [
            new byte[] { 0x89, 0x50, 0x4E, 0x47 }
        ],

        [".jpg"] =
        [
            new byte[] { 0xFF, 0xD8, 0xFF }
        ],

        [".jpeg"] =
        [
            new byte[] { 0xFF, 0xD8, 0xFF }
        ],

        [".webp"] =
        [
            new byte[] { 0x52, 0x49, 0x46, 0x46 }
        ]
    };

    public static bool IsValidImage(IFormFile file)
    {
        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        if (!Signatures.TryGetValue(extension, out var signatures))
            return false;

        using var stream = file.OpenReadStream();

        var header = new byte[12];

        stream.Read(header, 0, header.Length);

        return signatures.Any(signature =>
            header.Take(signature.Length)
                .SequenceEqual(signature));
    }
}