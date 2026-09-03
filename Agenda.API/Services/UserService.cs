using System.Net.Mail;
using Agenda.API.Models;

namespace Agenda.API.Services;

public sealed class UserService
{
    private readonly PasswordService _passwords;

    public UserService(PasswordService passwords)
    {
        _passwords = passwords;
    }

    public string? ValidateRegistration(string name, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return "Preencha nome, e-mail e senha.";
        }

        if (name.Trim().Length > 80)
        {
            return "O nome deve ter no maximo 80 caracteres.";
        }

        if (!IsValidEmail(email))
        {
            return "Informe um e-mail valido.";
        }

        return _passwords.Validate(password);
    }

    public string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    public UserAccount CreateUser(string name, string email, string password)
    {
        var salt = _passwords.CreateSalt();

        return new UserAccount(
            Guid.NewGuid(),
            name.Trim(),
            email,
            salt,
            _passwords.Hash(password, salt),
            DateTimeOffset.UtcNow
        );
    }

    public bool IsPasswordValid(UserAccount user, string password)
    {
        return _passwords.Verify(password, user.PasswordSalt, user.PasswordHash);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }
}
