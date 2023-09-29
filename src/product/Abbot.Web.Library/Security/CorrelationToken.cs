using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Base64UrlTextEncoder = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder;

namespace Serious.Abbot.Security;

public class CorrelationToken
{
    const int NonceLength = 32;
    const string CorrelationIdKey = ".xsrf";
    const string OrganizationIdKey = ".orgId";
    const string OAuthActionKey = ".action";

    public AuthenticationProperties Properties { get; }

    public DateTimeOffset IssuedUtc
    {
        get => Properties.IssuedUtc.Require();
        set => Properties.IssuedUtc = value;
    }

    public DateTimeOffset ExpiresUtc
    {
        get => Properties.ExpiresUtc.Require();
        set => Properties.ExpiresUtc = value;
    }

    public string Nonce
    {
        get => Properties.Items[CorrelationIdKey].Require();
        set => Properties.Items[CorrelationIdKey] = value;
    }

    public int OrganizationId
    {
        get => int.Parse(Properties.Items[OrganizationIdKey].Require(),
            CultureInfo.InvariantCulture);
        set => Properties.Items[OrganizationIdKey] = $"{value}";
    }

    public OAuthAction OAuthAction
    {
        get => Enum.Parse<OAuthAction>(Properties.Items[OAuthActionKey].Require());
        set => Properties.Items[OAuthActionKey] = value.ToString();
    }

    public CorrelationToken(DateTimeOffset issuedAt, DateTimeOffset expiresAt, OAuthAction action, int organizationId)
    {
        Properties = new()
        {
            IssuedUtc = issuedAt,
            ExpiresUtc = expiresAt,
            Items =
            {
                [CorrelationIdKey] = GenerateNonce(),
                [OrganizationIdKey] = organizationId.ToString(CultureInfo.InvariantCulture),
                [OAuthActionKey] = action.ToString()
            },
        };
    }

    CorrelationToken(AuthenticationProperties properties)
    {
        Properties = properties;
    }

    static string GenerateNonce()
    {
        var buffer = new byte[NonceLength];
        RandomNumberGenerator.Fill(buffer);
        return Base64UrlTextEncoder.Encode(buffer);
    }

    public static bool TryDecode(AuthenticationProperties properties, [MaybeNullWhen(false)] out CorrelationToken token)
    {
        if (properties.IssuedUtc is null || properties.ExpiresUtc is null || !properties.Items.ContainsKey(CorrelationIdKey))
        {
            token = null;
            return false;
        }

        token = new CorrelationToken(properties);
        return true;
    }
}

public enum OAuthAction
{
    Install,
    Connect,
    InstallCustom,
}

public interface ICorrelationService
{
    /// <summary>
    /// Encodes a correlation token with the provided information and returns the encoded value for use in the OAuth state parameter.
    /// </summary>
    /// <param name="context">An <see cref="HttpContext"/> to use when setting the XSRF cookie.</param>
    /// <param name="correlationCookieName">The name of the XSRF cookie.</param>
    /// <param name="issuedUtc">The time at which the token was issued (usually the current UTC time)</param>
    /// <param name="expiresUtc">The time at which the token should expire (usually 15 minutes after <paramref name="issuedUtc"/>)</param>
    /// <param name="action">The action we're performing with OAuth such as an install or connecting an identity.</param>
    /// <param name="organizationId">The organization ID to associate with the token.</param>
    /// <returns></returns>
    string EncodeAndSetNonceCookie(
        HttpContext context,
        string correlationCookieName,
        DateTimeOffset issuedUtc,
        DateTimeOffset expiresUtc,
        OAuthAction action,
        int organizationId);

    /// <summary>
    /// Attempts to validate the provided correlation token and returns the organization ID if there is one.
    /// </summary>
    /// <param name="context">An <see cref="HttpContext"/> to use to read the XSRF cookie.</param>
    /// <param name="token">The encoded Correlation Token.</param>
    /// <param name="correlationCookieName">The name of the XSRF cookie.</param>
    /// <param name="now">The current UTC time.</param>
    /// <param name="correlationToken">
    /// If the token is invalid (return value is false), this will be <c>null</c>.
    /// If the token is valid (return value is true), this will contain the token.
    /// </param>
    /// <returns>A boolean indicating if the token is valid.</returns>
    bool TryValidate(
        HttpContext context,
        string? token,
        string correlationCookieName,
        DateTimeOffset now,
        [NotNullWhen(true)] out CorrelationToken? correlationToken);
}

public class CorrelationService : ICorrelationService
{
    readonly SecureDataFormat<AuthenticationProperties> _secureDataFormat;

    public CorrelationService(IDataProtectionProvider provider)
    {
        _secureDataFormat = new SecureDataFormat<AuthenticationProperties>(
            new PropertiesSerializer(),
            provider.CreateProtector("CorrelationToken"));
    }

    public string EncodeAndSetNonceCookie(
        HttpContext context,
        string correlationCookieName,
        DateTimeOffset issuedUtc,
        DateTimeOffset expiresUtc,
        OAuthAction action,
        int organizationId)
    {
        var token = new CorrelationToken(issuedUtc, expiresUtc, action, organizationId);
        context.Response.Cookies.Append(correlationCookieName,
            token.Nonce,
            new()
            {
                HttpOnly = true,
                Secure = true,

                // This is essential for the OAuth flow to work.
                // It's completely random data with no tracking information in it, just used like an Anti-CSRF token.
                IsEssential = true,
            });

        var encoded = _secureDataFormat.Protect(token.Properties);
        return encoded;
    }

    public bool TryValidate(
        HttpContext context,
        string? token,
        string correlationCookieName,
        DateTimeOffset now,
        [NotNullWhen(true)] out CorrelationToken? correlationToken)
    {
        // It's convenient to allow the token to be null, but it's not valid.
        if (token is not { Length: > 0 })
        {
            correlationToken = null;
            return false;
        }

        // Read and delete the correlation cookie
        var correlationCookie = context.Request.Cookies[correlationCookieName];
        context.Response.Cookies.Delete(correlationCookieName);

        // Decode the token
        var properties = _secureDataFormat.Unprotect(token);
        if (properties == null)
        {
            correlationToken = null;
            return false;
        }

        if (!CorrelationToken.TryDecode(properties, out correlationToken))
        {
            correlationToken = null;
            return false;
        }

        // Validate expiry
        if (correlationToken.ExpiresUtc < now)
        {
            correlationToken = null;
            return false;
        }

        // Validate issue date (just in case)
        if (correlationToken.IssuedUtc > now)
        {
            // Token from the future?!
            correlationToken = null;
            return false;
        }

        // Validate the nonce
        if (correlationCookie != correlationToken.Nonce)
        {
            correlationToken = null;
            return false;
        }

        // We're good, correlation Token is set, go ahead and return true
        return true;
    }
}
