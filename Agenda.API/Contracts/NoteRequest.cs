namespace Agenda.API.Contracts;

public sealed record NoteRequest(string? Title, string? Body, DateOnly? NoteDate, TimeOnly? NoteTime, string? Color, bool IsCompleted);
