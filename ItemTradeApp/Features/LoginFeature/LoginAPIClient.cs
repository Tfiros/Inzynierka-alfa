using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ItemTradeApp.ExceptionsHandling;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public interface ILoginAPIClient
{
    Task<Result<string>> PostJsonAsync(string path, object payload, CancellationToken ct = default);
    Task<Result<string>> PostFormAsync(string path, Dictionary<string, string> form, CancellationToken ct = default);
    string BaseUrl { get; }
}

public class LoginAPIClient : ILoginAPIClient
{
    private readonly IHttpClientFactory _httpFactory;
    public string BaseUrl { get; }
    
    public LoginAPIClient(IHttpClientFactory httpFactory, IOptions<ItemTradeApp.LoginFeature.Auth0Options> opts)
    {
        _httpFactory = httpFactory;
        var domain = opts.Value.Domain?.Trim().TrimEnd('/')
                     ?? throw new InvalidOperationException("Auth0:Domain is missing");
        BaseUrl = domain.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? domain : $"https://{domain}";
    }
    
    public async Task<Result<string>> PostJsonAsync(string path, object payload, CancellationToken ct = default)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path));
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? Result<string>.Ok(body)
                : Result<string>.Fail(new AppError((int)resp.StatusCode, body, resp.ReasonPhrase));
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new AppError(502, ex.Message, "http_request_failed"));
        }
    }

    public async Task<Result<string>> PostFormAsync(string path, Dictionary<string, string> form, CancellationToken ct = default)
    {
        try
        {
            var http = _httpFactory.CreateClient();
            using var req = new HttpRequestMessage(HttpMethod.Post, Combine(BaseUrl, path))
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? Result<string>.Ok(body)
                : Result<string>.Fail(new AppError((int)resp.StatusCode, body, resp.ReasonPhrase));
        }
        catch (HttpRequestException ex)
        {
            return Result<string>.Fail(new AppError(502, ex.Message, "http_request_failed"));
        }
    }
    #region HELPERS
    private static string Combine(string baseUrl, string path)
        => $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
    #endregion

}