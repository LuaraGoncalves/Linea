using System.Net;
using System.Text.Json;
using Agenda.API.Contracts;
using Agenda.API.Data;
using Agenda.API.Models;
using Npgsql;

namespace Agenda.API.Services;

public sealed class AgendaStore
{
    private readonly NoteService _notes;
    private readonly NoteRepository _noteRepository;
    private readonly UserRepository _userRepository;
    private readonly UserService _users;
    private readonly string? _connectionString;
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
    private bool _schemaReady;

    public AgendaStore(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        UserService users,
        NoteService notes,
        NoteRepository noteRepository,
        UserRepository userRepository
    )
    {
        _users = users;
        _notes = notes;
        _noteRepository = noteRepository;
        _userRepository = userRepository;
        _connectionString = ResolveConnectionString(configuration);

        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);
        _filePath = Path.Combine(dataFolder, "agenda-data.json");
    }

    public async Task<(bool Success, string Message, UserAccount? User)> CreateUserAsync(string name, string email, string password)
    {
        var validationMessage = _users.ValidateRegistration(name, email, password);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            var normalizedEmail = _users.NormalizeEmail(email);

            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();

                if (await _userRepository.ExistsAsync(connection, normalizedEmail))
                {
                    return (false, "Este e-mail ja foi cadastrado.", null);
                }

                var user = _users.CreateUser(name, normalizedEmail, password);
                await _userRepository.InsertAsync(connection, user);
                return (true, "Conta criada.", user);
            }

            var data = await LoadAsync();

            if (data.Users.Any(user => user.Email == normalizedEmail))
            {
                return (false, "Este e-mail ja foi cadastrado.", null);
            }

            var localUser = _users.CreateUser(name, normalizedEmail, password);
            data.Users.Add(localUser);
            await SaveAsync(data);

            return (true, "Conta criada.", localUser);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task EnsureTestUserAsync(TestUserOptions options)
    {
        await _lock.WaitAsync();
        try
        {
            var normalizedEmail = _users.NormalizeEmail(options.Email);

            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();

                if (!await _userRepository.ExistsAsync(connection, normalizedEmail))
                {
                    await _userRepository.InsertAsync(connection, _users.CreateUser(options.Name, normalizedEmail, options.Password));
                }

                return;
            }

            var data = await LoadAsync();

            if (data.Users.Any(user => user.Email == normalizedEmail))
            {
                return;
            }

            data.Users.Add(_users.CreateUser(options.Name, normalizedEmail, options.Password));
            await SaveAsync(data);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<UserAccount?> ValidateUserAsync(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        await _lock.WaitAsync();
        try
        {
            var normalizedEmail = _users.NormalizeEmail(email);
            UserAccount? user;

            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();
                user = await _userRepository.GetByEmailAsync(connection, normalizedEmail);
            }
            else
            {
                var data = await LoadAsync();
                user = data.Users.FirstOrDefault(item => item.Email == normalizedEmail);
            }

            if (user is null)
            {
                return null;
            }

            return _users.IsPasswordValid(user, password)
                ? user
                : null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<AgendaNote>> GetNotesAsync(Guid userId)
    {
        await _lock.WaitAsync();
        try
        {
            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();
                return await _noteRepository.GetByUserAsync(connection, userId);
            }

            var data = await LoadAsync();
            return data.Notes
                .Where(note => note.UserId == userId)
                .OrderBy(note => note.NoteDate)
                .ThenBy(note => note.NoteTime)
                .ThenByDescending(note => note.UpdatedAt)
                .ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(bool Success, string Message, AgendaNote? Note)> CreateNoteAsync(Guid userId, NoteRequest request)
    {
        var validationMessage = _notes.Validate(request);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            var note = _notes.Create(userId, request);

            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();
                await _noteRepository.InsertAsync(connection, note);
                return (true, "Anotacao criada.", note);
            }

            var data = await LoadAsync();
            data.Notes.Add(note);
            await SaveAsync(data);
            return (true, "Anotacao criada.", note);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<(bool Success, string Message, AgendaNote? Note)> UpdateNoteAsync(Guid userId, Guid id, NoteRequest request)
    {
        var validationMessage = _notes.Validate(request);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();
                var updated = await _noteRepository.UpdateAsync(
                    connection,
                    userId,
                    id,
                    _notes.CleanTitle(request.Title),
                    _notes.CleanBody(request.Body),
                    request.NoteDate,
                    request.NoteTime,
                    _notes.CleanColor(request.Color),
                    request.IsCompleted,
                    DateTimeOffset.UtcNow
                );
                return updated is null
                    ? (false, "Anotacao nao encontrada.", null)
                    : (true, "Anotacao atualizada.", updated);
            }

            var data = await LoadAsync();
            var index = data.Notes.FindIndex(note => note.Id == id && note.UserId == userId);

            if (index < 0)
            {
                return (false, "Anotacao nao encontrada.", null);
            }

            var localUpdated = _notes.Update(data.Notes[index], request);

            data.Notes[index] = localUpdated;
            await SaveAsync(data);
            return (true, "Anotacao atualizada.", localUpdated);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> DeleteNoteAsync(Guid userId, Guid id)
    {
        await _lock.WaitAsync();
        try
        {
            if (UsesDatabase)
            {
                await EnsureSchemaAsync();
                await using var connection = await OpenConnectionAsync();
                return await _noteRepository.DeleteAsync(connection, userId, id);
            }

            var data = await LoadAsync();
            var removed = data.Notes.RemoveAll(note => note.Id == id && note.UserId == userId) > 0;

            if (removed)
            {
                await SaveAsync(data);
            }

            return removed;
        }
        finally
        {
            _lock.Release();
        }
    }

    private bool UsesDatabase => !string.IsNullOrWhiteSpace(_connectionString);

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaReady)
        {
            return;
        }

        await using var connection = await OpenConnectionAsync();
        await using var command = new NpgsqlCommand(
            """
            create table if not exists users (
                id uuid primary key,
                name varchar(80) not null,
                email varchar(120) not null unique,
                password_salt text not null,
                password_hash text not null,
                created_at timestamptz not null
            );

            create table if not exists notes (
                id uuid primary key,
                user_id uuid not null references users(id) on delete cascade,
                title varchar(100) not null,
                body text not null,
                note_date date null,
                note_time time null,
                color varchar(20) not null,
                is_completed boolean not null default false,
                created_at timestamptz not null,
                updated_at timestamptz not null
            );

            create index if not exists ix_notes_user_schedule
                on notes(user_id, note_date, note_time, updated_at desc);
            """,
            connection
        );

        await command.ExecuteNonQueryAsync();
        _schemaReady = true;
    }

    private async Task<AgendaData> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new AgendaData();
        }

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<AgendaData>(stream, _jsonOptions) ?? new AgendaData();
    }

    private async Task SaveAsync(AgendaData data)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, data, _jsonOptions);
    }

    private static string? ResolveConnectionString(IConfiguration configuration)
    {
        var value = configuration.GetConnectionString("Postgres")
            ?? configuration["DATABASE_URL"]
            ?? configuration["DatabaseUrl"];

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase)
                ? ConvertDatabaseUrl(value)
                : value;
    }

    private static string ConvertDatabaseUrl(string databaseUrl)
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = WebUtility.UrlDecode(userInfo.ElementAtOrDefault(0) ?? string.Empty),
            Password = WebUtility.UrlDecode(userInfo.ElementAtOrDefault(1) ?? string.Empty),
            SslMode = SslMode.Require
        };

        return builder.ConnectionString;
    }
}
