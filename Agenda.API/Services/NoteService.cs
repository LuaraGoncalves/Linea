using Agenda.API.Contracts;
using Agenda.API.Models;

namespace Agenda.API.Services;

public sealed class NoteService
{
    private static readonly string[] AllowedColors = ["paper", "rose", "sage", "blue"];

    public string? Validate(NoteRequest request)
    {
        if (request.Title?.Length > 100)
        {
            return "O titulo deve ter no maximo 100 caracteres.";
        }

        if (request.Body?.Length > 3000)
        {
            return "Os detalhes devem ter no maximo 3000 caracteres.";
        }

        if (!AllowedColors.Contains(CleanColor(request.Color)))
        {
            return "Escolha uma cor valida.";
        }

        return null;
    }

    public AgendaNote Create(Guid userId, NoteRequest request)
    {
        var now = DateTimeOffset.UtcNow;

        return new AgendaNote(
            Guid.NewGuid(),
            userId,
            CleanTitle(request.Title),
            CleanBody(request.Body),
            request.NoteDate,
            request.NoteTime,
            CleanColor(request.Color),
            request.IsCompleted,
            now,
            now
        );
    }

    public AgendaNote Update(AgendaNote note, NoteRequest request)
    {
        return note with
        {
            Title = CleanTitle(request.Title),
            Body = CleanBody(request.Body),
            NoteDate = request.NoteDate,
            NoteTime = request.NoteTime,
            Color = CleanColor(request.Color),
            IsCompleted = request.IsCompleted,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public string CleanTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? "Sem titulo" : title.Trim();
    }

    public string CleanBody(string? body)
    {
        return body?.Trim() ?? string.Empty;
    }

    public string CleanColor(string? color)
    {
        return string.IsNullOrWhiteSpace(color) ? "paper" : color.Trim();
    }
}
