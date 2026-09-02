using Agenda.API.Contracts;
using Agenda.API.Services;

namespace Agenda.API.Endpoints;

public static class NoteEndpoints
{
    public static IEndpointRouteBuilder MapNoteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/notes");

        group.MapGet("/", async (HttpRequest request, AgendaStore store, TokenService tokens) =>
        {
            var userId = tokens.GetUserId(request);

            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var notes = await store.GetNotesAsync(userId.Value);
            return Results.Ok(notes);
        });

        group.MapPost("/", async (HttpRequest request, NoteRequest note, AgendaStore store, TokenService tokens) =>
        {
            var userId = tokens.GetUserId(request);

            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.CreateNoteAsync(userId.Value, note);
            return result.Success
                ? Results.Created($"/api/notes/{result.Note!.Id}", result.Note)
                : Results.BadRequest(new { message = result.Message });
        });

        group.MapPut("/{id:guid}", async (HttpRequest request, Guid id, NoteRequest note, AgendaStore store, TokenService tokens) =>
        {
            var userId = tokens.GetUserId(request);

            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var result = await store.UpdateNoteAsync(userId.Value, id, note);

            if (result.Success)
            {
                return Results.Ok(result.Note);
            }

            return result.Note is null && result.Message.Contains("encontrada")
                ? Results.NotFound(new { message = result.Message })
                : Results.BadRequest(new { message = result.Message });
        });

        group.MapDelete("/{id:guid}", async (HttpRequest request, Guid id, AgendaStore store, TokenService tokens) =>
        {
            var userId = tokens.GetUserId(request);

            if (userId is null)
            {
                return Results.Unauthorized();
            }

            var removed = await store.DeleteNoteAsync(userId.Value, id);
            return removed ? Results.NoContent() : Results.NotFound();
        });

        return app;
    }
}
