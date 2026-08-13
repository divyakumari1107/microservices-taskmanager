using System.Collections.Concurrent;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Notification Service API",
        Version = "v1",
        Description = "Receives and stores notifications sent by other microservices"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification Service v1"));

// In-memory notification log.
var notifications = new ConcurrentBag<Notification>();

app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "NotificationService", timestamp = DateTime.UtcNow }));

// List all notifications - handy for demoing that TaskService's calls actually arrived.
app.MapGet("/api/notifications", () => Results.Ok(notifications.OrderByDescending(n => n.CreatedAt)));

// Filter by recipient - simulates "get my notifications"
app.MapGet("/api/notifications/user/{userId:guid}", (Guid userId) =>
    Results.Ok(notifications.Where(n => n.RecipientUserId == userId).OrderByDescending(n => n.CreatedAt)));

app.MapPost("/api/notifications", (NotificationRequest req, ILogger<Program> logger) =>
{
    var notification = new Notification
    {
        Id = Guid.NewGuid(),
        RecipientUserId = req.RecipientUserId,
        Message = req.Message,
        CreatedAt = DateTime.UtcNow
    };
    notifications.Add(notification);

    // In a real system this would send an email/push/SMS. For the demo we just log it.
    logger.LogInformation("[NOTIFY] To user {UserId}: {Message}", notification.RecipientUserId, notification.Message);

    return Results.Created($"/api/notifications/{notification.Id}", notification);
});

app.Run();

// --- Types ---
class Notification
{
    public Guid Id { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

record NotificationRequest(Guid RecipientUserId, string Message);
