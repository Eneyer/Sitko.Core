using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sitko.Core.App;

namespace Sitko.Core.Grpc.Client
{
    public interface IGrpcClientModule<TClient> where TClient : ClientBase<TClient>
    {
    }

    public abstract class GrpcClientModule<TClient, TResolver> : BaseApplicationModule<GrpcClientModuleConfig>,
        IGrpcClientModule<TClient>
        where TClient : ClientBase<TClient> where TResolver : class, IGrpcServiceAddressResolver<TClient>
    {
        public GrpcClientModule(GrpcClientModuleConfig config, Application application) : base(config, application)
        {
        }

        public override void ConfigureServices(IServiceCollection services, IConfiguration configuration,
            IHostEnvironment environment)
        {
            base.ConfigureServices(services, configuration, environment);
            if (Config.EnableHttp2UnencryptedSupport)
            {
                AppContext.SetSwitch(
                    "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            services.AddSingleton<IGrpcClientProvider<TClient>, GrpcClientProvider<TClient>>();
            services.AddSingleton<IGrpcServiceAddressResolver<TClient>, TResolver>();
            services.AddGrpcClient<TClient>((provider, options) =>
            {
                var resolver = provider.GetService<IGrpcServiceAddressResolver<TClient>>();
                options.Address = resolver.GetAddress();
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new HttpClientHandler();
                if (Config.DisableCertificatesValidation)
                {
                    handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                }

                return handler;
            });
            services.AddHealthChecks()
                .AddCheck<GrpcClientHealthCheck<TClient>>($"GRPC Client check: {typeof(TClient)}");
        }

        public override async Task InitAsync(IServiceProvider serviceProvider, IConfiguration configuration,
            IHostEnvironment environment)
        {
            await base.InitAsync(serviceProvider, configuration, environment);
            var resolver = serviceProvider.GetService<IGrpcServiceAddressResolver<TClient>>();
            await resolver.InitAsync();
        }
    }

    public class GrpcClientModuleConfig
    {
        public bool EnableHttp2UnencryptedSupport { get; set; }
        public bool DisableCertificatesValidation { get; set; }
    }
}
