namespace Agenda.API.Services;

public sealed class TestUserOptions
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "Usuario Teste";
    public string Email { get; set; } = "teste@agenda.local";
    public string Password { get; set; } = "123456";
}
