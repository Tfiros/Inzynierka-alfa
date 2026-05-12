using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Images;

public interface IImageService
{
    Task<string> UploadAsync(
        IFormFile file,
        string folder,
        CancellationToken ct = default);

    Task DeleteAsync(
        string url,
        CancellationToken ct = default);
    
    string GetPresignedUrl(string url);
}

public sealed class ImageService : IImageService
{
    private readonly IAmazonS3 s3;
    private readonly S3Config config;

    public ImageService(
        IAmazonS3 s3,
        IOptions<S3Config> options)
    {
        this.s3 = s3;
        config = options.Value;
    }

    public async Task<string> UploadAsync(
        IFormFile file,
        string folder,
        CancellationToken ct = default)
    {
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        if (file.Length == 0)
            throw new ArgumentException("Plik jest pusty.", nameof(file));

        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder nie może być pusty.", nameof(folder));

        if (!ImageExtensionValidator.IsValidImage(file))
            throw new ArgumentException("Nieprawidłowy plik obrazu.", nameof(file));

        var extension = Path
            .GetExtension(file.FileName)
            .ToLowerInvariant();

        var key =
            $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = config.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await s3.PutObjectAsync(request, ct);

        return BuildUrl(key);
    }

    public async Task DeleteAsync(
        string url,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        var key = ExtractKeyFromUrl(url);

        await s3.DeleteObjectAsync(
            config.BucketName,
            key,
            ct);
    }

    private string BuildUrl(string key)
    {
        return
            $"https://{config.BucketName}.s3.{config.Region}.amazonaws.com/{key}";
    }

    private static string ExtractKeyFromUrl(string url)
    {
        var uri = new Uri(url);

        return uri.AbsolutePath.TrimStart('/');
    }
    
    
    public string GetPresignedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return string.Empty;

        var key = ExtractKeyFromUrl(url);

        var request = new GetPreSignedUrlRequest
        {
            BucketName = config.BucketName,
            Key = key,
            Expires = DateTime.UtcNow.AddMinutes(15)
        };

        return s3.GetPreSignedURL(request);
    }
}