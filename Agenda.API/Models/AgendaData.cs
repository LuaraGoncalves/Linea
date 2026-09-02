namespace Agenda.API.Models;

public sealed class AgendaData
{
    public List<UserAccount> Users { get; set; } = [];
    public List<AgendaNote> Notes { get; set; } = [];
}
