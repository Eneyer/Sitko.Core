using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Sitko.Core.App;

namespace Sitko.Core.Health.Telegram;

public class TelegramHealthReporterModule : BaseApplicationModule<TelegramHealthReporterModuleOptions>
{
    public override string OptionsKey => "Health:Telegram";

    public override void ConfigureServices(IApplicationContext context, IServiceCollection services,
        TelegramHealthReporterModuleOptions startupOptions)
    {
        base.ConfigureServices(context, services, startupOptions);
        services.Configure<HealthCheckPublisherOptions>(_ => { });
        services.AddHealthChecks();
        services.AddHttpClient();
        services.AddSingleton<IHealthCheckPublisher, TelegramHealthCheckPublisher>();
    }
}
