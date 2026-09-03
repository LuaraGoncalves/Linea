using Agenda.API.Models;
using Npgsql;

namespace Agenda.API.Data;

public sealed class UserRepository
{
    public async Task<bool> ExistsAsync(NpgsqlConnection connection, string email)
    {
        await using var command = new NpgsqlCommand("select exists(select 1 from users where email = @email)", connection);
        command.Parameters.AddWithValue("email", email);
        return (bool)(await command.ExecuteScalarAsync() ?? false);
    }

    public async Task InsertAsync(NpgsqlConnection connection, UserAccount user)
    {
        await using var command = new NpgsqlCommand(
            """
            insert into users (id, name, email, password_salt, password_hash, created_at)
            values (@id, @name, @email, @password_salt, @password_hash, @created_at)
            """,
            connection
        );

        command.Parameters.AddWithValue("id", user.Id);
        command.Parameters.AddWithValue("name", user.Name);
        command.Parameters.AddWithValue("email", user.Email);
        command.Parameters.AddWithValue("password_salt", user.PasswordSalt);
        command.Parameters.AddWithValue("password_hash", user.PasswordHash);
        command.Parameters.AddWithValue("created_at", user.CreatedAt);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<UserAccount?> GetByEmailAsync(NpgsqlConnection connection, string email)
    {
        await using var command = new NpgsqlCommand(
            """
            select id, name, email, password_salt, password_hash, created_at
            from users
            where email = @email
            """,
            connection
        );
        command.Parameters.AddWithValue("email", email);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? ReadUser(reader) : null;
    }

    private static UserAccount ReadUser(NpgsqlDataReader reader)
    {
        return new UserAccount(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTimeOffset>(5)
        );
    }
}
