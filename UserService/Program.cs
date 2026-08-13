using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "User Service API",
        Version = "v1",
        Description = "Manages users for the Task Manager microservices demo"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "User Service v1"));

// In-memory "database" - thread-safe dictionary keyed by user Id.
// In a production system this would be replaced with EF Core + SQL Server/PostgreSQL.
var users = new ConcurrentDictionary<Guid, User>();

// Seed a couple of demo users so TaskService has something to reference immediately.
var seedUser1 = new User { Id = Guid.NewGuid(), Name = "Aditi Sharma", Email = "aditi.sharma@example.com", Department = "Engineering" };
var seedUser2 = new User { Id = Guid.NewGuid(), Name = "Rohan Verma", Email = "rohan.verma@example.com", Department = "QA" };
users[seedUser1.Id] = seedUser1;
users[seedUser2.Id] = seedUser2;

app.Logger.LogInformation("Seeded users: {Id1} ({Name1}), {Id2} ({Name2})",
    seedUser1.Id, seedUser1.Name, seedUser2.Id, seedUser2.Name);

// --- Health check (used by Docker healthcheck and Kubernetes probes) ---
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "UserService", timestamp = DateTime.UtcNow }));

// --- CRUD endpoints ---
app.MapGet("/api/users", () => Results.Ok(users.Values.OrderBy(u => u.Name)));

app.MapGet("/api/users/{id:guid}", (Guid id) =>
    users.TryGetValue(id, out var user) ? Results.Ok(user) : Results.NotFound(new { error = $"User {id} not found" }));

app.MapPost("/api/users", (CreateUserRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Email))
    {
        return Results.BadRequest(new { error = "Name and Email are required" });
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Name = req.Name,
        Email = req.Email,
        Department = req.Department ?? "Unassigned"
    };
    users[user.Id] = user;
    return Results.Created($"/api/users/{user.Id}", user);
});

app.MapPut("/api/users/{id:guid}", (Guid id, CreateUserRequest req) =>
{
    if (!users.ContainsKey(id)) return Results.NotFound(new { error = $"User {id} not found" });

    var updated = new User { Id = id, Name = req.Name, Email = req.Email, Department = req.Department ?? "Unassigned" };
    users[id] = updated;
    return Results.Ok(updated);
});

app.MapDelete("/api/users/{id:guid}", (Guid id) =>
    users.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound(new { error = $"User {id} not found" }));

app.Run();

// --- Types ---
class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

record CreateUserRequest(string Name, string Email, string? Department);
