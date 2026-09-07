using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using SalekhPos.Authorization.Application;
using SalekhPos.Identity.Application.Sessions;
using SalekhPos.Identity.Infrastructure.Tokens;

namespace SalekhPos.Api.Authentication;

public sealed record AuthenticationState(bool IsConfigured);

public static class AuthenticationSetup
{
    public static void AddPlatformAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"];
        var audience = configuration["Authentication:Audience"];
        var configured = !string.IsNullOrWhiteSpace(authority) && !string.IsNullOrWhiteSpace(audience);
        if ((!string.IsNullOrEmpty(authority) || !string.IsNullOrEmpty(audience))
            && (!configured || !Uri.TryCreate(authority, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
                || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || authority!.Length > 2048 || audience!.Length > 256 || audience.Any(char.IsControl)))
        {
            throw new InvalidOperationException("Authentication requires an HTTPS authority and a nonempty API audience.");
        }
        services.AddSingleton(new AuthenticationState(configured));
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = configured ? authority : null;
            options.Audience = configured ? audience : null;
            options.RequireHttpsMetadata = true;
            options.MapInboundClaims = false;
            options.SaveToken = false;
            options.IncludeErrorDetails = false;
            options.BackchannelTimeout = TimeSpan.FromSeconds(10);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authority,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                // Asymmetric provider keys only. Explicitly exclude none and HMAC.
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256],
                // Provider selection must establish an access-token type contract.
                ValidTypes = ["at+jwt"]
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    if (!configured || context.Request.Headers.Authorization.ToString().Length > 16384)
                    {
                        context.Fail("Authentication is unavailable or the credential is invalid.");
                    }
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var issuers = context.Principal!.FindAll("iss").ToArray();
                    var subjects = context.Principal.FindAll("sub").ToArray();
                    if (issuers.Length != 1 || subjects.Length != 1)
                    {
                        context.Fail("A unique subject and issuer are required.");
                        return;
                    }
                    try
                    {
                        _ = new AccessIdentity(issuers[0].Value, subjects[0].Value);
                    }
                    catch (ArgumentException)
                    {
                        context.Fail("The identity is invalid.");
                        return;
                    }
                    if (context.SecurityToken is not JsonWebToken token)
                    {
                        context.Fail("The credential format is unsupported.");
                        return;
                    }
                    var credential = new AuthenticatedCredential(issuers[0].Value, subjects[0].Value,
                        token.EncodedToken, token.ValidTo);
                    var revocations = context.HttpContext.RequestServices.GetRequiredService<TokenRevocations>();
                    if (await revocations.IsRevokedAsync(credential, context.HttpContext.RequestAborted))
                    {
                        context.Fail("The credential is unavailable.");
                        return;
                    }
                    context.HttpContext.Items[typeof(AuthenticatedCredential)] = credential;
                }
            };
        });
        services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
    }
}
