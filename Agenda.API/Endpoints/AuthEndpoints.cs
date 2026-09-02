using Agenda.API.Contracts;
using Agenda.API.Services;

namespace Agenda.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");

        group.MapPost("/register", async (AuthRequest request, AgendaStore store, TokenService tokens) =>
        {
            var result = await store.CreateUserAsync(request.Name, request.Email, request.Password);

            if (!result.Success)
            {
                return Results.BadRequest(new { message = result.Message });
            }

            var token = tokens.Create(result.User!);
            return Results.Ok(AuthResponse.From(result.User!, token));
        });

        group.MapPost("/login", async (LoginRequest request, AgendaStore store, TokenService tokens) =>
        {
            var user = await store.ValidateUserAsync(request.Email, request.Password);

            if (user is null)
            {
                return Results.Unauthorized();
            }

            var token = tokens.Create(user);
            return Results.Ok(AuthResponse.From(user, token));
        });

        return app;
    }
}
