using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReviewOrchestrator.Core;
using ReviewOrchestrator.Core.Engines;
using ReviewOrchestrator.Infrastructure.Application;
using ReviewOrchestrator.Infrastructure.Background;
using ReviewOrchestrator.Infrastructure.Notifications;
using ReviewOrchestrator.Infrastructure.Persistence;
using ReviewOrchestrator.Infrastructure.ReviewBriefs;
using ReviewOrchestrator.Infrastructure.SourceControl;

namespace ReviewOrchestrator.Api;

public static class ServiceRegistration
{
    public static IServiceCollection AddReviewOrchestrator(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ReviewRoutingOptions>(configuration.GetSection(ReviewRoutingOptions.Section));
        services.Configure<QueueOptions>(configuration.GetSection(QueueOptions.Section));
        services.Configure<PriorityOptions>(configuration.GetSection(PriorityOptions.Section));
        services.Configure<ReviewSurfaceOptions>(configuration.GetSection(ReviewSurfaceOptions.Section));
        services.Configure<SourceControlOptions>(configuration.GetSection(SourceControlOptions.Section));
        services.Configure<ReviewBriefOptions>(configuration.GetSection(ReviewBriefOptions.Section));

        services.AddSingleton(TimeProvider.System);

        services.AddDbContext<ReviewHubDbContext>(o =>
            o.UseSqlite(configuration.GetConnectionString("ReviewHub") ?? "Data Source=reviewhub.db"));

        // Domain engines are stateless; options are read once at startup.
        services.AddSingleton(sp =>
        {
            T Opt<T>() where T : class => sp.GetRequiredService<IOptions<T>>().Value;
            var surface = new ReviewSurfaceEvaluator(Opt<ReviewSurfaceOptions>());
            return new OrchestrationEngines(
                new RiskEngine(surface),
                new PriorityEngine(Opt<PriorityOptions>()),
                new ReviewerRouter(),
                surface,
                new WipEvaluator(Opt<ReviewRoutingOptions>()),
                new AgingCalculator(Opt<QueueOptions>()),
                new LeasePolicy(Opt<ReviewRoutingOptions>()));
        });

        services.AddSingleton<ISourceControlProvider>(sp =>
            sp.GetRequiredService<IOptions<SourceControlOptions>>().Value.Provider.ToLowerInvariant() switch
            {
                "mock" => new MockSourceControlProvider(sp.GetRequiredService<TimeProvider>()),
                "azuredevops" => new AzureDevOpsSourceControlProvider(),
                "github" => new GitHubSourceControlProvider(),
                var other => throw new InvalidOperationException($"Unknown SourceControl:Provider '{other}'.")
            });

        services.AddSingleton<IReviewBriefProvider>(sp =>
            sp.GetRequiredService<IOptions<ReviewBriefOptions>>().Value.Provider.ToLowerInvariant() switch
            {
                "mock" => new MockReviewBriefProvider(sp.GetRequiredService<TimeProvider>()),
                "openai" => new OpenAiReviewBriefProvider(),
                var other => throw new InvalidOperationException($"Unknown ReviewBrief:Provider '{other}'.")
            });

        services.AddSingleton<IReviewNotificationService, LoggingReviewNotificationService>();
        services.AddScoped<ReviewOrchestrationService>();
        services.AddHostedService<LeaseExpirationService>();

        return services;
    }
}
