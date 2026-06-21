using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.Features.Shared.Images;

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
        
        const long maxFileSize = 5 * 1024 * 1024;
        
        if (file is null)
            throw new ArgumentNullException(nameof(file));

        if (file.Length == 0)
            throw new ArgumentException("Plik jest pusty.", nameof(file));

        if (string.IsNullOrWhiteSpace(folder))
            throw new ArgumentException("Folder nie może być pusty.", nameof(folder));
        
        if (file.Length > maxFileSize)
            throw new ArgumentException("File size exceeded.");

        if (!ImageExtensionValidator.IsImageValid(
                file,
                out var extension,
                out var contentType))
        {
            throw new ArgumentException("Nieprawidłowy plik obrazu.", nameof(file));
        }

        var key =
            $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = config.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = contentType
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

    private string ExtractKeyFromUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            throw new ArgumentException("Nieprawidłowy adres pliku.", nameof(url));

        var expectedHost =
            $"{config.BucketName}.s3.{config.Region}.amazonaws.com";

        if (!string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Adres pliku nie należy do skonfigurowanego bucketu.", nameof(url));

        var key = uri.AbsolutePath.TrimStart('/');

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Nieprawidłowy klucz pliku.", nameof(url));

        return Uri.UnescapeDataString(key);
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