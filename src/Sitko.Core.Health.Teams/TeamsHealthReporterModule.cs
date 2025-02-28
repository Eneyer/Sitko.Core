using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sitko.Core.App;

namespace Sitko.Core.Health.Teams;

public class TeamsHealthReporterModule : BaseApplicationModule<TeamsHealthReporterModuleOptions>
{
    public override string OptionsKey => "Health:Teams";

    public override void ConfigureServices(IApplicationContext context, IServiceCollection services,
        TeamsHealthReporterModuleOptions startupOptions)
    {
        base.ConfigureServices(context, services, startupOptions);
        services.Configure<HealthCheckPublisherOptions>(_ => { });
        services.AddHealthChecks();
        services.AddHttpClient();
        services.AddSingleton<IHealthCheckPublisher, TeamsHealthCheckPublisher>();
    }
}
