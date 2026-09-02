using Agenda.API.Models;
using Agenda.API.Services;

namespace Agenda.API.Contracts;

public sealed record AuthResponse(string Token, DateTimeOffset ExpiresAt, UserResponse User)
{
    public static AuthResponse From(UserAccount user, TokenResult token)
    {
        return new AuthResponse(token.Value, token.ExpiresAt, new UserResponse(user.Id, user.Name, user.Email));
    }
}
