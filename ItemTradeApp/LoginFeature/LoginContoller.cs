// src/ItemTradeApp/LoginFeature/LoginController.cs
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ItemTradeApp.LoginFeature;

public record LoginRequest(string Username, string Password);

[ApiController]
[Route("[controller]")]
public class LoginController : ControllerBase
{
    private readonly Auth0Options _cfg;
    private readonly IHttpClientFactory _http;

    public LoginController(IOptions<Auth0Options> cfg, IHttpClientFactory http)
    {
        _cfg = cfg.Value;
        _http = http;
    }

    [HttpPost]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "username_or_password_missing" });

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "http://auth0.com/oauth/grant-type/password-realm",
            ["realm"] = _cfg.Realm,
            ["username"] = req.Username,
            ["password"] = req.Password,
            ["client_id"] = _cfg.ClientId,
            ["client_secret"] = _cfg.ClientSecret,
            ["audience"] = _cfg.Audience,
            ["scope"] = "openid profile email"
        };

        var http = _http.CreateClient();
        var resp = await http.PostAsync($"https://{_cfg.Domain}/oauth/token", new FormUrlEncodedContent(form));
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            return StatusCode((int)resp.StatusCode, new { error = "auth0_token_failed", details = json });

        using var doc = JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var expEl) ? expEl.GetInt32() : 0;

        if (string.IsNullOrEmpty(accessToken))
            return StatusCode(502, new { error = "no_access_token_from_auth0" });

        return Ok(new { accessToken, expiresIn });
    }
}
