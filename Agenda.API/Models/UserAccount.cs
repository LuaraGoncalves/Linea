namespace Agenda.API.Models;

public sealed record UserAccount(
    Guid Id,
    string Name,
    string Email,
    string PasswordSalt,
    string PasswordHash,
    DateTimeOffset CreatedAt
);
