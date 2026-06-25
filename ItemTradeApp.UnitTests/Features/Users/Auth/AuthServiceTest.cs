using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using ItemTradeApp.ApiResultHandling;
using ItemTradeApp.Features.Users.Auth;
using ItemTradeApp.Features.Users.Auth.DTOs.RequestDtos;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration;
using ItemTradeApp.Features.Users.Shared.AuthZeroIntegration.DTOs.Response;
using ItemTradeApp.Features.Users.Shared.DTOs;
using ItemTradeApp.Persistence.Models;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ItemTradeApp.Persistence;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace ItemTradeApp.UnitTests.Features.Users.Auth;

[TestSubject(typeof(AuthService))]
public class AuthServiceTest
{
    private readonly Mock<IAuthZeroAPIClient> _apiClient = new();
    private readonly Mock<IAuthRepository> _authRepository = new();
    private readonly Mock<ILogger<AuthService>> _logger = new();
    private readonly Mock<IAuthZeroManagementClient> _managementClient = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly AuthService _service;

    public AuthServiceTest()
    {
        var options = Options.Create(new AuthZeroOptions
        {
            Realm = "Username-Password-Authentication",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Audience = "audience"
        });

        _service = new AuthService(
            options,
            _apiClient.Object,
            _authRepository.Object,
            _managementClient.Object,
            _unitOfWork.Object,
            _logger.Object);
    }
    private void SetupSuccessfulTransaction()
    {
        var transaction = new Mock<IDbContextTransaction>();

        transaction
            .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        transaction
            .Setup(x => x.DisposeAsync())
            .Returns(ValueTask.CompletedTask);

        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }
    [Fact]
    public async Task LoginAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.LoginAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WhenAuth0ReturnsBadRequest_ReturnsInvalidEmailOrPassword()
    {
        var req = new LoginRequest("test@test.com", "wrong");

        _apiClient
            .Setup(x => x.PasswordRealmTokenAsync(
                req.Email,
                req.Password,
                "Username-Password-Authentication",
                "client-id",
                "client-secret",
                "audience",
                "openid profile email offline_access",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.BadRequest("provider error"));

        var result = await _service.LoginAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Invalid email or password.", result.Message);

        _authRepository.Verify(x => x.GetUserByEmail(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LoginAsync_WhenRawResponseIsInvalidJson_ReturnsInternalServerError()
    {
        var req = new LoginRequest("test@test.com", "pass");

        _apiClient
            .Setup(x => x.PasswordRealmTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw("{ invalid json")));

        var result = await _service.LoginAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Login response was invalid.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenAccessTokenIsMissing_ReturnsInternalServerError()
    {
        var req = new LoginRequest("test@test.com", "pass");

        var raw = JsonSerializer.Serialize(new
        {
            expires_in = 3600,
            refresh_token = "refresh-token",
            id_token = "id-token"
        });

        _apiClient
            .Setup(x => x.PasswordRealmTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(Auth0ResponseWithRaw(raw)));

        var result = await _service.LoginAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Login response was invalid.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenLocalUserDoesNotExist_ReturnsNotFound()
    {
        var req = new LoginRequest("test@test.com", "pass");

        _apiClient
            .Setup(x => x.PasswordRealmTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw(TokenRawResponse("access-token", 3600, "refresh-token", "id-token"))));

        _authRepository
            .Setup(x => x.GetUserByEmail(req.Email))
            .ReturnsAsync((User?)null);

        var result = await _service.LoginAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User account was not found.", result.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenValid_ReturnsLoginResponse()
    {
        var req = new LoginRequest("test@test.com", "pass");

        _apiClient
            .Setup(x => x.PasswordRealmTokenAsync(
                req.Email,
                req.Password,
                "Username-Password-Authentication",
                "client-id",
                "client-secret",
                "audience",
                "openid profile email offline_access",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw(TokenRawResponse("access-token", 3600, "refresh-token", "id-token"))));

        _authRepository
            .Setup(x => x.GetUserByEmail(req.Email))
            .ReturnsAsync(new User
            {
                ID = 123,
                Email = req.Email
            });

        var result = await _service.LoginAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Login successful", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data!.Id);
        Assert.Equal(3600, result.Data.ExpiresIn);
        Assert.Equal("access-token", result.Data.AccessToken);
        Assert.Equal("refresh-token", result.Data.RefreshToken);
    }

    [Fact]
    public async Task RefreshAsync_WhenRefreshTokenIsMissing_ReturnsUnauthorized()
    {
        var result = await _service.RefreshAsync("   ", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing refresh token.", result.Message);

        _apiClient.Verify(x => x.RefreshTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshAsync_WhenAuth0ReturnsUnauthorized_ReturnsSessionExpired()
    {
        _apiClient
            .Setup(x => x.RefreshTokenAsync(
                "refresh-token",
                "client-id",
                "client-secret",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Unauthorized("provider error"));

        var result = await _service.RefreshAsync("refresh-token", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Session expired.", result.Message);
    }

    [Fact]
    public async Task RefreshAsync_WhenAccessTokenIsInvalidJwt_ReturnsInternalServerError()
    {
        _apiClient
            .Setup(x => x.RefreshTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw(TokenRawResponse("not-a-jwt", 3600, "refresh-token", "id-token"))));

        var result = await _service.RefreshAsync("refresh-token", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Refresh response was invalid.", result.Message);
    }

    [Fact]
    public async Task RefreshAsync_WhenLocalUserDoesNotExist_ReturnsNotFound()
    {
        var accessToken = CreateJwt("auth0|abc");

        _apiClient
            .Setup(x => x.RefreshTokenAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw(TokenRawResponse(accessToken, 3600, "new-refresh-token", "id-token"))));

        _authRepository
            .Setup(x => x.GetUserByAuth0Id("abc"))
            .ReturnsAsync((User?)null);

        var result = await _service.RefreshAsync("refresh-token", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User account was not found.", result.Message);
    }

    [Fact]
    public async Task RefreshAsync_WhenValid_ReturnsRefreshResponse()
    {
        var accessToken = CreateJwt("auth0|abc");

        _apiClient
            .Setup(x => x.RefreshTokenAsync(
                "refresh-token",
                "client-id",
                "client-secret",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
                Auth0ResponseWithRaw(TokenRawResponse(accessToken, 3600, "new-refresh-token", "id-token"))));

        _authRepository
            .Setup(x => x.GetUserByAuth0Id("abc"))
            .ReturnsAsync(new User
            {
                ID = 123,
                Auth0UserID = "abc"
            });

        var result = await _service.RefreshAsync("refresh-token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Refresh successful", result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(123, result.Data!.Id);
        Assert.Equal(3600, result.Data.ExpiresIn);
        Assert.Equal(accessToken, result.Data.AccessToken);
        Assert.Equal("new-refresh-token", result.Data.RefreshToken);
    }

    [Fact]
    public async Task LogoutAsync_WhenRefreshTokenIsMissing_ReturnsBadRequest()
    {
        var result = await _service.LogoutAsync("   ", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Missing refresh token.", result.Message);

        _apiClient.Verify(x => x.RevokeRefreshTokenAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAsync_WhenAuth0ReturnsInternalServerError_ReturnsServiceUnavailable()
    {
        _apiClient
            .Setup(x => x.RevokeRefreshTokenAsync(
                "refresh-token",
                "client-id",
                "client-secret",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.InternalServerError("provider error"));

        var result = await _service.LogoutAsync("refresh-token", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Logout service unavailable.", result.Message);
    }

    [Fact]
    public async Task LogoutAsync_WhenValid_ReturnsSuccessMessage()
    {
        _apiClient
            .Setup(x => x.RevokeRefreshTokenAsync(
                "refresh-token",
                "client-id",
                "client-secret",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse()));

        var result = await _service.LogoutAsync("refresh-token", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Logout successful", result.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ForgotPasswordAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenAuth0ReturnsBadRequest_ReturnsPasswordResetFailed()
    {
        var req = new ForgotPasswordRequest("test@test.com");

        _apiClient
            .Setup(x => x.ChangePasswordAsync(
                req.Email,
                "Username-Password-Authentication",
                "client-id",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.BadRequest("provider error"));

        var result = await _service.ForgotPasswordAsync(req, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Password reset failed.", result.Message);
    }

    [Fact]
    public async Task ForgotPasswordAsync_WhenValid_ReturnsResetEmailSent()
    {
        var req = new ForgotPasswordRequest("test@test.com");

        _apiClient
            .Setup(x => x.ChangePasswordAsync(
                req.Email,
                "Username-Password-Authentication",
                "client-id",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse()));

        var result = await _service.ForgotPasswordAsync(req, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Reset email sent", result.Message);
    }

   [Fact]
    public async Task RegisterAsync_WhenRequestIsNull_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.RegisterAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task RegisterAsync_WhenAuth0ReturnsConflict_ReturnsUserAlreadyExists()
    {
    var req = new RegisterRequest(
        Email: "test@test.com",
        Password: "Password123!",
        DateTime.Now,
        Username: "test");

    _managementClient
        .Setup(x => x.CreateUserAsync(
            req.Email,
            req.Password,
            "Username-Password-Authentication",
            req.Username,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Conflict("provider error"));

    var result = await _service.RegisterAsync(req, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal("User with this email already exists.", result.Message);

    _authRepository.Verify(x => x.Register(
        It.IsAny<RegisterRequest>(),
        It.IsAny<string>()), Times.Never);

    _unitOfWork.Verify(x => x.BeginTransactionAsync(
        It.IsAny<CancellationToken>()), Times.Never);
}

    [Fact]
    public async Task RegisterAsync_WhenRawResponseDoesNotContainUserId_ReturnsInternalServerError()
    {
    var req = new RegisterRequest(
        Email: "test@test.com",
        Password: "Password123!",
        DateTime.Now,
        Username: "test");

    _managementClient
        .Setup(x => x.CreateUserAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
            Auth0ResponseWithRaw("""{"email":"test@test.com"}""")));

    var result = await _service.RegisterAsync(req, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal("Registration response was invalid.", result.Message);

    _authRepository.Verify(x => x.Register(
        It.IsAny<RegisterRequest>(),
        It.IsAny<string>()), Times.Never);

    _unitOfWork.Verify(x => x.BeginTransactionAsync(
        It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenValid_RegistersLocalUserSendsVerificationEmailAndReturnsSuccess()
    {
    var req = new RegisterRequest(
        "test@test.com",
        "Password123!",
        DateTime.Now,
        "test");

    SetupSuccessfulTransaction();

    _managementClient
        .Setup(x => x.CreateUserAsync(
            req.Email,
            req.Password,
            "Username-Password-Authentication",
            req.Username,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
            Auth0ResponseWithRaw("""{"user_id":"auth0|abc"}""")));

    _managementClient
        .Setup(x => x.SendVerificationEmailAsync(
            "auth0|abc",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse()));

    var result = await _service.RegisterAsync(req, CancellationToken.None);

    Assert.True(result.IsSuccess);
    Assert.Equal("Registration successful", result.Message);

    _authRepository.Verify(x => x.Register(
        req,
        "abc"), Times.Once);

    _unitOfWork.Verify(x => x.BeginTransactionAsync(
        It.IsAny<CancellationToken>()), Times.Once);

    _unitOfWork.Verify(x => x.SaveChangesAsync(
        It.IsAny<CancellationToken>()), Times.Once);

    _managementClient.Verify(x => x.SendVerificationEmailAsync(
        "auth0|abc",
        It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenVerificationEmailFails_ReturnsFailure()
    {
    var req = new RegisterRequest(
        "test@test.com",
        "Password123!",
        DateTime.Now,
        "test");

    SetupSuccessfulTransaction();

    _managementClient
        .Setup(x => x.CreateUserAsync(
            req.Email,
            req.Password,
            "Username-Password-Authentication",
            req.Username,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
            Auth0ResponseWithRaw("""{"user_id":"auth0|abc"}""")));

    _managementClient
        .Setup(x => x.SendVerificationEmailAsync(
            "auth0|abc",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.InternalServerError("verification_email_failed"));

    var result = await _service.RegisterAsync(req, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal("verification_email_failed", result.Message);

    _authRepository.Verify(x => x.Register(
        req,
        "abc"), Times.Once);

    _unitOfWork.Verify(x => x.SaveChangesAsync(
        It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WhenLocalRegistrationFails_DeletesAuth0UserAndReturnsInternalServerError()
    {
    var req = new RegisterRequest(
        "test@test.com",
        "Password123!",
        DateTime.Now,
        "test");

    var transaction = new Mock<IDbContextTransaction>();

    transaction
        .Setup(x => x.DisposeAsync())
        .Returns(ValueTask.CompletedTask);

    _unitOfWork
        .Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(transaction.Object);

    _managementClient
        .Setup(x => x.CreateUserAsync(
            req.Email,
            req.Password,
            "Username-Password-Authentication",
            req.Username,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(
            Auth0ResponseWithRaw("""{"user_id":"auth0|abc"}""")));

    _authRepository
        .Setup(x => x.Register(req, "abc"))
        .ThrowsAsync(new InvalidOperationException("db error"));

    _managementClient
        .Setup(x => x.DeleteUserAsync(
            "auth0|abc",
            It.IsAny<CancellationToken>()))
        .ReturnsAsync(Result<AuthZeroBodyResponse>.Success(new AuthZeroBodyResponse()));

    var result = await _service.RegisterAsync(req, CancellationToken.None);

    Assert.False(result.IsSuccess);
    Assert.Equal("Registration failed.", result.Message);

    _managementClient.Verify(x => x.DeleteUserAsync(
        "auth0|abc",
        It.IsAny<CancellationToken>()), Times.Once);

    _managementClient.Verify(x => x.SendVerificationEmailAsync(
        It.IsAny<string>(),
        It.IsAny<CancellationToken>()), Times.Never);
    }
    [Fact]
    public async Task GetUserIdAsync_WhenAuth0IdIsMissing_ReturnsBadRequest()
    {
        var result = await _service.GetUserIdAsync("   ", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("Auth0 user id is required.", result.Message);
    }

    [Fact]
    public async Task GetUserIdAsync_WhenUserDoesNotExist_ReturnsNotFound()
    {
        _authRepository
            .Setup(x => x.GetUserByAuth0Id("abc"))
            .ReturnsAsync((User?)null);

        var result = await _service.GetUserIdAsync("auth0|abc", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("User not found.", result.Message);
    }

    [Fact]
    public async Task GetUserIdAsync_WhenUserExists_ReturnsUserId()
    {
        _authRepository
            .Setup(x => x.GetUserByAuth0Id("abc"))
            .ReturnsAsync(new User
            {
                ID = 123,
                Auth0UserID = "abc"
            });

        var result = await _service.GetUserIdAsync("auth0|abc", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(123, result.Data);
        Assert.Equal("User id retrieved successfully", result.Message);
    }

    private static string TokenRawResponse(
        string accessToken,
        int expiresIn,
        string? refreshToken,
        string? idToken)
    {
        return JsonSerializer.Serialize(new
        {
            access_token = accessToken,
            expires_in = expiresIn,
            refresh_token = refreshToken,
            id_token = idToken
        });
    }

    private static string CreateJwt(string sub)
    {
        var token = new JwtSecurityToken(
            claims: new[]
            {
                new Claim("sub", sub)
            });

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static AuthZeroBodyResponse Auth0ResponseWithRaw(string rawResponse)
    {
        var response = Activator.CreateInstance<AuthZeroBodyResponse>();

        var detailsProperty = typeof(AuthZeroBodyResponse).GetProperty("Details")
            ?? throw new InvalidOperationException("AuthZeroBodyResponse.Details property not found.");

        var details = detailsProperty.GetValue(response);

        if (details is null)
        {
            details = Activator.CreateInstance(detailsProperty.PropertyType)
                ?? throw new InvalidOperationException("Could not create AuthZeroBodyResponse.Details instance.");

            detailsProperty.SetValue(response, details);
        }

        var rawResponseProperty = detailsProperty.PropertyType.GetProperty("RawResponse")
            ?? throw new InvalidOperationException("Details.RawResponse property not found.");

        rawResponseProperty.SetValue(details, rawResponse);

        return response;
    }
}