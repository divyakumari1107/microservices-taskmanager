using System.Collections.Concurrent;
using System.Net.Http.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Task Service API",
        Version = "v1",
        Description = "Manages tasks; talks to UserService and NotificationService over REST"
    });
});

// Base URLs for the other microservices come from configuration / environment variables,
// e.g. Services__UserService=http://user-service:8080 (set in docker-compose.yml / k8s manifests).
// Local defaults let you run all three services with "dotnet run" without Docker too.
var userServiceUrl = builder.Configuration["Services:UserService"] ?? "http://localhost:5001";
var notificationServiceUrl = builder.Configuration["Services:NotificationService"] ?? "http://localhost:5003";

builder.Services.AddHttpClient("UserService", client =>
{
    client.BaseAddress = new Uri(userServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("NotificationService", client =>
{
    client.BaseAddress = new Uri(notificationServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Task Service v1"));

app.Logger.LogInformation("TaskService starting. UserService={UserUrl} NotificationService={NotifyUrl}",
    userServiceUrl, notificationServiceUrl);

// In-memory "database" of tasks.
var tasks = new ConcurrentDictionary<Guid, TaskItem>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "TaskService", timestamp = DateTime.UtcNow }));

app.MapGet("/api/tasks", () => Results.Ok(tasks.Values.OrderByDescending(t => t.CreatedAt)));

app.MapGet("/api/tasks/{id:guid}", (Guid id) =>
    tasks.TryGetValue(id, out var task) ? Results.Ok(task) : Results.NotFound(new { error = $"Task {id} not found" }));

// Creating a task requires the assigned user to actually exist -> demonstrates
// synchronous service-to-service REST calls, which is the core of this project.
app.MapPost("/api/tasks", async (CreateTaskRequest req, IHttpClientFactory httpFactory, ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(req.Title))
    {
        return Results.BadRequest(new { error = "Title is required" });
    }

    var userClient = httpFactory.CreateClient("UserService");
    HttpResponseMessage userResponse;
    try
    {
        userResponse = await userClient.GetAsync($"/api/users/{req.AssignedUserId}");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "UserService unreachable while validating user {UserId}", req.AssignedUserId);
        return Results.Problem("UserService is currently unavailable. Please try again later.", statusCode: 503);
    }

    if (!userResponse.IsSuccessStatusCode)
    {
        return Results.BadRequest(new { error = $"User {req.AssignedUserId} does not exist in UserService" });
    }

    var task = new TaskItem
    {
        Id = Guid.NewGuid(),
        Title = req.Title,
        Description = req.Description ?? string.Empty,
        AssignedUserId = req.AssignedUserId,
        Status = "Pending",
        CreatedAt = DateTime.UtcNow
    };
    tasks[task.Id] = task;

    // Notify - best effort. Task creation should not fail just because
    // NotificationService happens to be down (loose coupling between services).
    var notifyClient = httpFactory.CreateClient("NotificationService");
    var payload = new { recipientUserId = task.AssignedUserId, message = $"New task '{task.Title}' has been assigned to you." };
    try
    {
        await notifyClient.PostAsJsonAsync("/api/notifications", payload);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "NotificationService unreachable, continuing without blocking task creation");
    }

    return Results.Created($"/api/tasks/{task.Id}", task);
});

app.MapPut("/api/tasks/{id:guid}/status", async (Guid id, UpdateStatusRequest req, IHttpClientFactory httpFactory, ILogger<Program> logger) =>
{
    if (!tasks.TryGetValue(id, out var task))
    {
        return Results.NotFound(new { error = $"Task {id} not found" });
    }

    var validStatuses = new[] { "Pending", "InProgress", "Completed" };
    if (!validStatuses.Contains(req.Status))
    {
        return Results.BadRequest(new { error = $"Status must be one of: {string.Join(", ", validStatuses)}" });
    }

    task.Status = req.Status;
    tasks[id] = task;

    var notifyClient = httpFactory.CreateClient("NotificationService");
    var payload = new { recipientUserId = task.AssignedUserId, message = $"Task '{task.Title}' status changed to {req.Status}." };
    try
    {
        await notifyClient.PostAsJsonAsync("/api/notifications", payload);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "NotificationService unreachable, continuing without blocking status update");
    }

    return Results.Ok(task);
});

app.MapDelete("/api/tasks/{id:guid}", (Guid id) =>
    tasks.TryRemove(id, out _) ? Results.NoContent() : Results.NotFound(new { error = $"Task {id} not found" }));

app.Run();

// --- Types ---
class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid AssignedUserId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
}

record CreateTaskRequest(string Title, string? Description, Guid AssignedUserId);
record UpdateStatusRequest(string Status);
