namespace Agenda.API.Contracts;

public sealed record AuthRequest(string Name, string Email, string Password);
