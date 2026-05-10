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
        string key,
        CancellationToken ct = default);

    string GetImageUrl(string key);
}

public sealed class ImageService : IImageService
{
    private readonly IAmazonS3 s3;
    private readonly S3Config _config;

    public ImageService(
        IAmazonS3 s3,
        IOptions<S3Config> options)
    {
        this.s3 = s3;
        this._config = options.Value;
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

        var extension = Path.GetExtension(file.FileName);
        var key = $"{folder.Trim('/')}/{Guid.NewGuid():N}{extension}";

        await using var stream = file.OpenReadStream();

        var request = new PutObjectRequest
        {
            BucketName = _config.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType
        };

        await s3.PutObjectAsync(request, ct);

        return key;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        await s3.DeleteObjectAsync(_config.BucketName, key, ct);
    }

    public string GetImageUrl(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        return $"https://{_config.BucketName}.s3.{_config.Region}.amazonaws.com/{key}";
    }
}