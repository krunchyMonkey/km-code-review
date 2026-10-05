using System.Text.Json.Serialization;
using ReviewOrchestrator.Api;
using ReviewOrchestrator.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReviewOrchestrator(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

var resetOnStartup = builder.Configuration.GetValue("Database:ResetOnStartup", true);
await DatabaseInitializer.InitializeAsync(app.Services, resetOnStartup);

app.MapReviewOrchestratorApi();
app.MapGet("/", () => Results.Redirect("/api/health")).ExcludeFromDescription();

app.Run();

public partial class Program;
