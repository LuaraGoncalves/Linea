using System.Security.Cryptography;

namespace Agenda.API.Services;

public sealed class PasswordService
{
    public string? Validate(string password)
    {
        if (password.Length < 5 || password.Length > 80)
        {
            return "A senha deve ter entre 5 e 80 caracteres.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "A senha precisa ter pelo menos uma letra maiuscula, uma letra minuscula e um numero.";
        }

        return null;
    }

    public string CreateSalt()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
    }

    public string Hash(string password, string salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(salt),
            100_000,
            HashAlgorithmName.SHA256,
            32
        );

        return Convert.ToBase64String(hash);
    }

    public bool Verify(string password, string salt, string expectedHash)
    {
        var hash = Hash(password, salt);
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hash),
            Convert.FromBase64String(expectedHash)
        );
    }
}
