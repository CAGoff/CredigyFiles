using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

/// <summary>
/// Development-only authentication handler that creates a fake authenticated user.
/// Bypasses Entra ID JWT validation for local development against Azurite.
/// NEVER enable in production — controlled by Authentication:UseDevelopmentAuth config.
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = Context.RequestServices.GetRequiredService<IConfiguration>();
        var role = config["Authentication:DevRole"] ?? "SFT.Admin";
        var userId = config["Authentication:DevUserId"] ?? "dev-user-id";
        var userName = config["Authentication:DevUserName"] ?? "dev@localhost";

        var claims = new[]
        {
            new Claim("oid", userId),
            new Claim("preferred_username", userName),
            new Claim(ClaimTypes.Role, role),
        };

        var identity = new ClaimsIdentity(claims, "DevAuth");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "DevAuth");

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
