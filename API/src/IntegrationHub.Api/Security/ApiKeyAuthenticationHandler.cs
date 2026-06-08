using System.Security.Claims;
using System.Text.Encodings.Web;
using IntegrationHub.Application.Abstractions.Security;
using IntegrationHub.Shared.Configuration;
using IntegrationHub.Shared.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using AuthenticationOptions = IntegrationHub.Shared.Configuration.AuthenticationOptions;

namespace IntegrationHub.Api.Security;

/// <summary>
/// Authenticates machine-to-machine callers by validating the <c>X-Api-Key</c> header
/// against stored PBKDF2 hashes. Returns no-result when the header is absent so the
/// JWT scheme can be tried first; fails for present-but-unrecognized keys.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyValidator _validator;
    private readonly AuthenticationOptions _authOptions;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyValidator validator,
        IOptions<AuthenticationOptions> authOptions)
        : base(options, logger, encoder)
    {
        _validator = validator;
        _authOptions = authOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headerName = string.IsNullOrWhiteSpace(_authOptions.ApiKeyHeaderName)
            ? "X-Api-Key"
            : _authOptions.ApiKeyHeaderName;

        if (!Request.Headers.TryGetValue(headerName, out var presented) || string.IsNullOrWhiteSpace(presented))
        {
            // No API key supplied — let other schemes handle the request.
            return AuthenticateResult.NoResult();
        }

        var principal = await _validator.ValidateAsync(presented.ToString(), Context.RequestAborted);
        if (principal is null)
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypeNames.Subject, principal.Name),
            new(ClaimTypeNames.Role, principal.Role),
        };
        if (!string.IsNullOrWhiteSpace(principal.TenantId))
        {
            claims.Add(new Claim(ClaimTypeNames.ActiveTenantId, principal.TenantId));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypeNames.Subject, ClaimTypeNames.Role);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
