using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agenda.API.Models;

namespace Agenda.API.Services;

public sealed class TokenService
{
    private readonly JwtOptions _options;
    private readonly byte[] _secret;

    public TokenService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
        _secret = ResolveSecret(configuration, environment);
    }

    public TokenResult Create(UserAccount user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.ExpirationMinutes);

        var header = new Dictionary<string, object>
        {
            ["alg"] = "HS256",
            ["typ"] = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString(),
            ["name"] = user.Name,
            ["email"] = user.Email,
            ["iss"] = _options.Issuer,
            ["aud"] = _options.Audience,
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = expiresAt.ToUnixTimeSeconds(),
            ["jti"] = Guid.NewGuid().ToString()
        };

        var unsignedToken = $"{EncodeJson(header)}.{EncodeJson(payload)}";
        var signature = Sign(unsignedToken);

        return new TokenResult($"{unsignedToken}.{signature}", expiresAt);
    }

    public Guid? GetUserId(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Validate(header[prefix.Length..].Trim());
    }

    private Guid? Validate(string token)
    {
        var parts = token.Split('.');

        if (parts.Length != 3)
        {
            return null;
        }

        var unsignedToken = $"{parts[0]}.{parts[1]}";
        var expectedSignature = Sign(unsignedToken);

        if (!FixedTimeEquals(parts[2], expectedSignature))
        {
            return null;
        }

        try
        {
            using var payload = JsonDocument.Parse(Decode(parts[1]));
            var root = payload.RootElement;

            if (!IsExpectedText(root, "iss", _options.Issuer) ||
                !IsExpectedText(root, "aud", _options.Audience) ||
                !root.TryGetProperty("exp", out var expElement) ||
                expElement.GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds() ||
                !root.TryGetProperty("sub", out var subElement) ||
                !Guid.TryParse(subElement.GetString(), out var userId))
            {
                return null;
            }

            return userId;
        }
        catch
        {
            return null;
        }
    }

    private string Sign(string value)
    {
        using var hmac = new HMACSHA256(_secret);
        return Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    private static string EncodeJson(object value)
    {
        return Encode(JsonSerializer.SerializeToUtf8Bytes(value));
    }

    private static string Encode(byte[] value)
    {
        return Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Decode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left),
            Encoding.UTF8.GetBytes(right)
        );
    }

    private static bool IsExpectedText(JsonElement root, string propertyName, string expected)
    {
        return root.TryGetProperty(propertyName, out var element) &&
            string.Equals(element.GetString(), expected, StringComparison.Ordinal);
    }

    private static byte[] ResolveSecret(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var secret = configuration["Jwt:Secret"] ?? Environment.GetEnvironmentVariable("AGENDA_JWT_SECRET");

        if (!string.IsNullOrWhiteSpace(secret))
        {
            if (secret.Length < 32)
            {
                throw new InvalidOperationException("A chave JWT precisa ter pelo menos 32 caracteres.");
            }

            return Encoding.UTF8.GetBytes(secret);
        }

        if (environment.IsDevelopment())
        {
            return RandomNumberGenerator.GetBytes(32);
        }

        throw new InvalidOperationException("Configure a variavel de ambiente AGENDA_JWT_SECRET.");
    }
}

public sealed record TokenResult(string Value, DateTimeOffset ExpiresAt);

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "AgendaOnline";
    public string Audience { get; set; } = "AgendaOnline.Web";
    public int ExpirationMinutes { get; set; } = 120;
}
