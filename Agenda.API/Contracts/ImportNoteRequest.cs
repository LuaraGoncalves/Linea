namespace Agenda.API.Contracts;

public sealed record ImportNoteRequest(Guid LocalId, NoteRequest? Note);
