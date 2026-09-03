using Agenda.API.Models;
using Npgsql;

namespace Agenda.API.Data;

public sealed class NoteRepository
{
    public async Task<List<AgendaNote>> GetByUserAsync(NpgsqlConnection connection, Guid userId)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, user_id, title, body, note_date, note_time, color, is_completed, created_at, updated_at
            from notes
            where user_id = @user_id
            order by note_date nulls last, note_time nulls last, updated_at desc
            """,
            connection
        );
        command.Parameters.AddWithValue("user_id", userId);

        var notes = new List<AgendaNote>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            notes.Add(ReadNote(reader));
        }

        return notes;
    }

    public async Task InsertAsync(NpgsqlConnection connection, AgendaNote note)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into notes (id, user_id, title, body, note_date, note_time, color, is_completed, created_at, updated_at)
            values (@id, @user_id, @title, @body, @note_date, @note_time, @color, @is_completed, @created_at, @updated_at)
            """,
            connection
        );

        AddNoteParameters(command, note);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<AgendaNote?> UpdateAsync(
        NpgsqlConnection connection,
        Guid userId,
        Guid id,
        string title,
        string body,
        DateOnly? noteDate,
        TimeOnly? noteTime,
        string color,
        bool isCompleted,
        DateTimeOffset updatedAt
    )
    {
        await using var command = new NpgsqlCommand(
            """
            update notes
            set title = @title,
                body = @body,
                note_date = @note_date,
                note_time = @note_time,
                color = @color,
                is_completed = @is_completed,
                updated_at = @updated_at
            where id = @id and user_id = @user_id
            returning id, user_id, title, body, note_date, note_time, color, is_completed, created_at, updated_at
            """,
            connection
        );

        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("title", title);
        command.Parameters.AddWithValue("body", body);
        command.Parameters.AddWithValue("note_date", (object?)noteDate ?? DBNull.Value);
        command.Parameters.AddWithValue("note_time", (object?)noteTime ?? DBNull.Value);
        command.Parameters.AddWithValue("color", color);
        command.Parameters.AddWithValue("is_completed", isCompleted);
        command.Parameters.AddWithValue("updated_at", updatedAt);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadNote(reader) : null;
    }

    public async Task<bool> DeleteAsync(NpgsqlConnection connection, Guid userId, Guid id)
    {
        await using var command = new NpgsqlCommand(
            "delete from notes where id = @id and user_id = @user_id",
            connection
        );
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("user_id", userId);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    private static void AddNoteParameters(NpgsqlCommand command, AgendaNote note)
    {
        command.Parameters.AddWithValue("id", note.Id);
        command.Parameters.AddWithValue("user_id", note.UserId);
        command.Parameters.AddWithValue("title", note.Title);
        command.Parameters.AddWithValue("body", note.Body);
        command.Parameters.AddWithValue("note_date", (object?)note.NoteDate ?? DBNull.Value);
        command.Parameters.AddWithValue("note_time", (object?)note.NoteTime ?? DBNull.Value);
        command.Parameters.AddWithValue("color", note.Color);
        command.Parameters.AddWithValue("is_completed", note.IsCompleted);
        command.Parameters.AddWithValue("created_at", note.CreatedAt);
        command.Parameters.AddWithValue("updated_at", note.UpdatedAt);
    }

    private static AgendaNote ReadNote(NpgsqlDataReader reader)
    {
        return new AgendaNote(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<TimeOnly>(5),
            reader.GetString(6),
            reader.GetBoolean(7),
            reader.GetFieldValue<DateTimeOffset>(8),
            reader.GetFieldValue<DateTimeOffset>(9)
        );
    }
}
