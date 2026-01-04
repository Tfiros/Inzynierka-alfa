namespace ItemTradeApp;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

public sealed class PrefixDocumentFilter(string prefix) : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return;

        var newPaths = new OpenApiPaths();
        foreach (var (key, value) in swaggerDoc.Paths)
        {
            var newKey = key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? key
                : $"{prefix}{key}";

            newPaths.Add(newKey, value);
        }

        swaggerDoc.Paths = newPaths;
    }
}
