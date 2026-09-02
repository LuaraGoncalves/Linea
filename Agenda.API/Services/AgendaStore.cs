using System.Net.Mail;
using System.Security.Cryptography;
using System.Text.Json;
using Agenda.API.Contracts;
using Agenda.API.Models;

namespace Agenda.API.Services;

public sealed class AgendaStore
{
    private static readonly string[] AllowedColors = ["paper", "rose", "sage", "blue"];
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public AgendaStore(IWebHostEnvironment environment)
    {
        var dataFolder = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataFolder);
        _filePath = Path.Combine(dataFolder, "agenda-data.json");
    }

    public async Task<(bool Success, string Message, UserAccount? User)> CreateUserAsync(string name, string email, string password)
    {
        var validationMessage = ValidateUserInput(name, email, password);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            var data = await LoadAsync();
            var normalizedEmail = NormalizeEmail(email);

            if (data.Users.Any(user => user.Email == normalizedEmail))
            {
                return (false, "Este e-mail ja foi cadastrado.", null);
            }

            var user = CreateUser(name, normalizedEmail, password);
            data.Users.Add(user);
            await SaveAsync(data);

            return (true, "Conta criada.", user);
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
            var data = await LoadAsync();
            var normalizedEmail = NormalizeEmail(options.Email);

            if (data.Users.Any(user => user.Email == normalizedEmail))
            {
                return;
            }

            data.Users.Add(CreateUser(options.Name, normalizedEmail, options.Password));
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
            var data = await LoadAsync();
            var normalizedEmail = NormalizeEmail(email);
            var user = data.Users.FirstOrDefault(item => item.Email == normalizedEmail);

            if (user is null)
            {
                return null;
            }

            var hash = HashPassword(password, user.PasswordSalt);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(hash),
                Convert.FromBase64String(user.PasswordHash)
            )
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
        var validationMessage = ValidateNote(request);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            var data = await LoadAsync();
            var now = DateTimeOffset.UtcNow;
            var note = new AgendaNote(
                Guid.NewGuid(),
                userId,
                CleanTitle(request.Title),
                request.Body?.Trim() ?? string.Empty,
                request.NoteDate,
                request.NoteTime,
                CleanColor(request.Color),
                request.IsCompleted,
                now,
                now
            );

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
        var validationMessage = ValidateNote(request);

        if (validationMessage is not null)
        {
            return (false, validationMessage, null);
        }

        await _lock.WaitAsync();
        try
        {
            var data = await LoadAsync();
            var index = data.Notes.FindIndex(note => note.Id == id && note.UserId == userId);

            if (index < 0)
            {
                return (false, "Anotacao nao encontrada.", null);
            }

            var existing = data.Notes[index];
            var updated = existing with
            {
                Title = CleanTitle(request.Title),
                Body = request.Body?.Trim() ?? string.Empty,
                NoteDate = request.NoteDate,
                NoteTime = request.NoteTime,
                Color = CleanColor(request.Color),
                IsCompleted = request.IsCompleted,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            data.Notes[index] = updated;
            await SaveAsync(data);
            return (true, "Anotacao atualizada.", updated);
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

    private static UserAccount CreateUser(string name, string email, string password)
    {
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        return new UserAccount(
            Guid.NewGuid(),
            name.Trim(),
            email,
            salt,
            HashPassword(password, salt),
            DateTimeOffset.UtcNow
        );
    }

    private static string HashPassword(string password, string salt)
    {
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            Convert.FromBase64String(salt),
            100_000,
            HashAlgorithmName.SHA256,
            32
        );

        return Convert.ToBase64String(hash);
    }

    private static string? ValidateUserInput(string name, string email, string password)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return "Preencha nome, e-mail e senha.";
        }

        if (name.Trim().Length > 80)
        {
            return "O nome deve ter no maximo 80 caracteres.";
        }

        if (!IsValidEmail(email))
        {
            return "Informe um e-mail valido.";
        }

        if (password.Length < 6 || password.Length > 80)
        {
            return "A senha deve ter entre 6 e 80 caracteres.";
        }

        return null;
    }

    private static string? ValidateNote(NoteRequest request)
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

    private static bool IsValidEmail(string email)
    {
        try
        {
            return new MailAddress(email).Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string CleanTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title) ? "Sem titulo" : title.Trim();
    }

    private static string CleanColor(string? color)
    {
        return string.IsNullOrWhiteSpace(color) ? "paper" : color.Trim();
    }
}
