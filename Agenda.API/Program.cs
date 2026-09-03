using Agenda.API.Endpoints;
using Agenda.API.Services;

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AgendaWeb", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddSingleton<AgendaStore>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<NoteService>();
builder.Services.AddSingleton<TokenService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    var testUser = app.Configuration.GetSection("SeedTestUser").Get<TestUserOptions>();

    if (testUser?.Enabled == true)
    {
        await app.Services.GetRequiredService<AgendaStore>().EnsureTestUserAsync(testUser);
    }
}

app.UseCors("AgendaWeb");

app.MapAuthEndpoints();
app.MapNoteEndpoints();

app.Run();
