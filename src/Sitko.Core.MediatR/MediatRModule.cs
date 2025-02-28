using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Sitko.Core.App;

namespace Sitko.Core.MediatR;

public interface IMediatRModule
{
}

public class MediatRModule<TAssembly> : BaseApplicationModule<MediatRModuleOptions<TAssembly>>,
    IMediatRModule
{
    public override string OptionsKey => "MediatR";

    public override void ConfigureServices(IApplicationContext context, IServiceCollection services,
        MediatRModuleOptions<TAssembly> startupOptions)
    {
        base.ConfigureServices(context, services, startupOptions);
        services.AddMediatR(startupOptions.Assemblies.ToArray());
    }
}
