using RazorLight;

namespace ItemTradeApp.Features.Shared.Emails;

public interface IEmailTemplateRenderer
{
    Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default);
}

public sealed class RazorEmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly RazorLightEngine _engine;

    public RazorEmailTemplateRenderer(IWebHostEnvironment env)
    {
        var templatesPath = Path.Combine(
            env.ContentRootPath,
            "Resources",
            "EmailTemplates"
            );

        _engine = new RazorLightEngineBuilder()
            .UseFileSystemProject(templatesPath)
            .UseMemoryCachingProvider()
            .Build();
    }

    public Task<string> RenderAsync<TModel>(string templateName, TModel model, CancellationToken ct = default)
    {
        return _engine.CompileRenderAsync($"{templateName}.cshtml", model);
    }
}