using Amazon;
using Amazon.S3;

namespace ItemTradeApp.Features.Images;

public static class DI
{
    public static IServiceCollection RegisterImagesFeatureDi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<S3Config>(
            configuration.GetSection("AWS"));

        services.AddSingleton<IAmazonS3>(_ =>
        {
            var options = configuration
                .GetSection("AWS")
                .Get<S3Config>()!;

            return new AmazonS3Client(
                options.AccessKey,
                options.SecretKey,
                RegionEndpoint.GetBySystemName(options.Region)
            );
        });

        services.AddScoped<IImageService, ImageService>();

        return services;
    }
}