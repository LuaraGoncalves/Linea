namespace Agenda.API.Models;

public sealed record AgendaNote(
    Guid Id,
    Guid UserId,
    string Title,
    string Body,
    DateOnly? NoteDate,
    TimeOnly? NoteTime,
    string Color,
    bool IsCompleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
