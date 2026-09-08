using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SalekhPos.Authorization.Infrastructure;
using SalekhPos.Identity.Infrastructure.Tokens;

namespace SalekhPos.Api.Authentication;

public sealed record WebAuthenticationSettings(string Authority, string ClientId, string ClientSecret, string PublicOrigin, bool AllowLoopbackHttp)
{
    public override string ToString() => "Web authentication settings (credentials redacted)";
    public static WebAuthenticationSettings? Read(IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection("WebAuthentication");
        var authority = section["Authority"];
        var clientId = section["ClientId"];
        var secret = section["ClientSecret"];
        var origin = section["PublicOrigin"];
        if (new[] { authority, clientId, secret, origin }.All(string.IsNullOrEmpty)) { return null; }
        var allowHttp = environment.IsDevelopment() && section.GetValue<bool>("AllowLoopbackHttp");
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 256 || clientId.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(secret) || !ValidUrl(authority, allowHttp, false)
            || !ValidUrl(origin, allowHttp, true))
        {
            throw new InvalidOperationException("Web authentication requires a provider, confidential client and explicit public origin.");
        }
        return new(authority!.TrimEnd('/'), clientId, secret, origin!.TrimEnd('/'), allowHttp);
    }

    private static bool ValidUrl(string? value, bool allowHttp, bool originOnly) =>
        value is { Length: <= 2048 } && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == "https" || (allowHttp && uri.Scheme == "http" && uri.IsLoopback))
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (!originOnly || uri.AbsolutePath == "/");
}

public static class WebAuthentication
{
    public const string CookieScheme = "WebSession";
    public const string OidcScheme = "WebOidc";

    public static void AddWebAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = WebAuthenticationSettings.Read(configuration, environment);
        services.AddSingleton(new WebAuthenticationState(settings));
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = settings?.AllowLoopbackHttp == true ? "SalekhPos-Dev-Csrf" : "__Host-SalekhPos-Csrf";
            options.Cookie.SecurePolicy = settings?.AllowLoopbackHttp == true ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.HeaderName = "X-CSRF-Token";
        });
        if (settings is null) { return; }
        var protection = services.AddDataProtection().SetApplicationName("SalekhPos.Web");
        // Shared, certificate-protected keys are mandatory outside disposable environments.
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Testing"))
        {
            var directory = configuration["WebAuthentication:KeyRingDirectory"];
            var certificatePath = configuration["WebAuthentication:KeyCertificatePath"];
            if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)
                || string.IsNullOrWhiteSpace(certificatePath) || !Path.IsPathFullyQualified(certificatePath))
            {
                throw new InvalidOperationException("Production web sessions require an absolute shared key-ring directory and encryption certificate path.");
            }
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
                configuration["WebAuthentication:KeyCertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
            using var rsa = certificate.GetRSAPrivateKey();
            if (rsa is null || rsa.KeySize < 2048) { throw new InvalidOperationException("Session key encryption requires an RSA private key of at least 2048 bits."); }
            protection.PersistKeysToFileSystem(new DirectoryInfo(directory)).ProtectKeysWithCertificate(certificate);
        }
        services.AddSingleton(provider => new PostgresTicketStore(
            provider.GetRequiredService<AccessDatabase>().DataSource, provider.GetRequiredService<IDataProtectionProvider>()));
        services.AddOptions<CookieAuthenticationOptions>(CookieScheme).Configure<PostgresTicketStore>((options, store) => options.SessionStore = store);
        // Preserve Bearer as the default. Web cookies never authorize existing v1 APIs.
        services.AddAuthentication().AddCookie(CookieScheme, options =>
        {
            options.Cookie.Name = settings.AllowLoopbackHttp ? "SalekhPos-Dev-Session" : "__Host-SalekhPos-Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = settings.AllowLoopbackHttp ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
            options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
        }).AddOpenIdConnect(OidcScheme, options =>
        {
            options.Authority = settings.Authority;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.SignInScheme = CookieScheme;
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.RequireHttpsMetadata = !settings.AllowLoopbackHttp;
            options.MapInboundClaims = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.SaveTokens = true;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.CallbackPath = "/auth/callback";
            options.SignedOutCallbackPath = "/auth/signed-out";
            options.RemoteSignOutPath = "/auth/provider-signout";
            if (settings.AllowLoopbackHttp)
            {
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.NonceCookie.SameSite = SameSiteMode.Lax;
            }
            options.TokenValidationParameters.NameClaimType = "name";
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.ValidIssuer = settings.Authority;
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                context.ProtocolMessage.RedirectUri = settings.PublicOrigin + "/auth/callback";
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToIdentityProviderForSignOut = context =>
            {
                context.ProtocolMessage.PostLogoutRedirectUri = settings.PublicOrigin + "/auth/signed-out";
                return Task.CompletedTask;
            };
            options.Events.OnRemoteFailure = context =>
            {
                context.HandleResponse();
                context.Response.Redirect(settings.PublicOrigin + "/sign-in?error=authentication_failed");
                return Task.CompletedTask;
            };
        });
    }

    public static void MapWebAuthentication(this WebApplication app)
    {
        app.MapGet("/auth/session", async (HttpContext context, IAntiforgery antiforgery, WebAuthenticationState state) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var result = state.Settings is null ? null : await context.AuthenticateAsync(CookieScheme);
            var authenticated = result?.Succeeded == true;
            return Results.Ok(new
            {
                configured = state.Settings is not null,
                authenticated,
                name = authenticated ? result!.Principal?.FindFirst("name")?.Value : null,
                csrfToken = state.Settings is null ? null : antiforgery.GetAndStoreTokens(context).RequestToken
            });
        }).AllowAnonymous().RequireRateLimiting("business");
        app.MapPost("/auth/login", async (HttpContext context, IAntiforgery antiforgery, WebAuthenticationState state) =>
        {
            if (state.Settings is null) { return Results.StatusCode(503); }
            if (!await ValidateMutation(context, antiforgery, state.Settings)) { return Results.BadRequest(); }
            return Results.Challenge(new AuthenticationProperties { RedirectUri = state.Settings.PublicOrigin + "/dashboard" }, [OidcScheme]);
        }).AllowAnonymous().RequireRateLimiting("business");
        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery, WebAuthenticationState state) =>
        {
            if (state.Settings is null) { return Results.StatusCode(503); }
            if (!await ValidateMutation(context, antiforgery, state.Settings)) { return Results.BadRequest(); }
            // OIDC reads its encrypted server-side id_token before local ticket removal.
            await context.SignOutAsync(OidcScheme, new AuthenticationProperties { RedirectUri = state.Settings.PublicOrigin + "/sign-in" });
            await context.SignOutAsync(CookieScheme);
            return Results.Empty;
        }).AllowAnonymous().RequireRateLimiting("business");
    }

    private static async Task<bool> ValidateMutation(HttpContext context, IAntiforgery antiforgery, WebAuthenticationSettings settings)
    {
        if (!string.Equals(context.Request.Headers.Origin, settings.PublicOrigin, StringComparison.Ordinal)) { return false; }
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
}

public sealed record WebAuthenticationState(WebAuthenticationSettings? Settings);
