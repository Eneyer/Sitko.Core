using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Sitko.Core.App.Localization;
using Sitko.Core.App.Logging;
using Tempus;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Sitko.Core.App
{
    public abstract class Application : IApplication, IAsyncDisposable
    {
        private static readonly ConcurrentDictionary<Guid, Application> Apps = new();

        private static readonly string BaseConsoleLogFormat =
            "[{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}]{NewLine}\t{Message:lj}{NewLine}{Exception}";

        private readonly List<Action<ApplicationContext, HostBuilderContext, IConfigurationBuilder>>
            appConfigurationActions = new();

        private readonly string[] args;

        private readonly List<Action<ApplicationContext, LoggerConfiguration, LogLevelSwitcher>>
            loggerConfigurationActions = new();

        private readonly LogLevelSwitcher logLevelSwitcher = new();

        private readonly Dictionary<Type, ApplicationModuleRegistration> moduleRegistrations =
            new();

        private readonly List<Action<ApplicationContext, HostBuilderContext, IServiceCollection>>
            servicesConfigurationActions = new();

        private readonly Dictionary<string, object> store = new();
        public readonly Guid ____RULE_VIOLATION____Id____RULE_VIOLATION____ = Guid.NewGuid();

        public readonly bool ____RULE_VIOLATION____IsCheckRun____RULE_VIOLATION____;

        protected readonly Dictionary<string, LogEventLevel>
            ____RULE_VIOLATION____LogEventLevels____RULE_VIOLATION____ = new();

        private IHost? appHost;

        protected Application(string[] args)
        {
            this.args = args;
            Apps.TryAdd(____RULE_VIOLATION____Id____RULE_VIOLATION____, this);
            Console.OutputEncoding = Encoding.UTF8;
            if (args.Length > 0 && args[0] == "check")
            {
                ____RULE_VIOLATION____IsCheckRun____RULE_VIOLATION____ = true;
            }

            var loggerConfiguration = new LoggerConfiguration();
            loggerConfiguration
                .WriteTo.Console(outputTemplate: BaseConsoleLogFormat,
                    restrictedToMinimumLevel: LogEventLevel.Debug);
            InternalLogger = new SerilogLoggerFactory(loggerConfiguration.CreateLogger()).CreateLogger<Application>();
        }

        protected virtual string ConsoleLogFormat => BaseConsoleLogFormat;

        private ILogger<Application> InternalLogger { get; set; }

        public string Name { get; private set; } = "App";
        public string Version { get; private set; } = "dev";

        public virtual ValueTask DisposeAsync()
        {
            appHost?.Dispose();
            return new ValueTask();
        }

        protected IReadOnlyList<ApplicationModuleRegistration>
            GetEnabledModuleRegistrations(ApplicationContext context) => moduleRegistrations
            .Where(r => r.Value.IsEnabled(context)).Select(r => r.Value).ToList();

        public static Application GetApp(Guid id)
        {
            if (Apps.ContainsKey(id))
            {
                return Apps[id];
            }

            throw new ArgumentException($"Application {id} is not registered", nameof(id));
        }

        private void LogCheck(string message)
        {
            if (____RULE_VIOLATION____IsCheckRun____RULE_VIOLATION____)
            {
                InternalLogger.LogInformation("Check log: {Message}", message);
            }
        }

        private IHostBuilder ConfigureHostBuilder(Action<IHostBuilder>? configure = null)
        {
            LogCheck("Configure host builder start");

            LogCheck("Create tmp host builder");

            using var tmpHost = CreateHostBuilder(args)
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = false;
                    options.ValidateScopes = true;
                })
                .ConfigureLogging(builder => { builder.SetMinimumLevel(LogLevel.Information); }).Build();

            var tmpConfiguration = tmpHost.Services.GetRequiredService<IConfiguration>();
            var tmpEnvironment = tmpHost.Services.GetRequiredService<IHostEnvironment>();

            var name = GetName(tmpEnvironment, tmpConfiguration);
            Name = !string.IsNullOrEmpty(name) ? name : tmpEnvironment.ApplicationName;

            var version = GetVersion(tmpEnvironment, tmpConfiguration);
            if (!string.IsNullOrEmpty(version))
            {
                Version = version;
            }

            var tmpApplicationContext = GetContext(tmpEnvironment, tmpConfiguration);

            LogCheck("Init application");

            InitApplication();

            LogCheck("Create main host builder");

            var hostBuilder = CreateHostBuilder(args)
                .UseDefaultServiceProvider(options =>
                {
                    options.ValidateOnBuild = true;
                    options.ValidateScopes = true;
                })
                .ConfigureHostConfiguration(builder =>
                {
                    builder.AddJsonFile("appsettings.json", true, false);
                    builder.AddJsonFile($"appsettings.{tmpApplicationContext.Environment.EnvironmentName}.json", true,
                        false);
                })
                .ConfigureAppConfiguration((context, builder) =>
                {
                    LogCheck("Configure app configuration");
                    var appContext = GetContext(context.HostingEnvironment, context.Configuration);
                    foreach (var appConfigurationAction in appConfigurationActions)
                    {
                        appConfigurationAction(appContext, context, builder);
                    }

                    LogCheck("Configure app configuration in modules");
                    foreach (var moduleRegistration in GetEnabledModuleRegistrations(tmpApplicationContext))
                    {
                        moduleRegistration.ConfigureAppConfiguration(appContext, context, builder);
                    }
                })
                .ConfigureServices((context, services) =>
                {
                    LogCheck("Configure app services");
                    services.AddSingleton(logLevelSwitcher);
                    services.AddSingleton<ILoggerFactory>(_ => new SerilogLoggerFactory());
                    services.AddSingleton(typeof(IApplication), this);
                    services.AddSingleton(typeof(Application), this);
                    services.AddSingleton(GetType(), this);
                    services.AddHostedService<ApplicationLifetimeService>();
                    services.AddTransient<IScheduler, Scheduler>();
                    services.AddTransient(typeof(ILocalizationProvider<>), typeof(LocalizationProvider<>));

                    var appContext = GetContext(context.HostingEnvironment, context.Configuration);
                    foreach (var servicesConfigurationAction in servicesConfigurationActions)
                    {
                        servicesConfigurationAction(appContext, context, services);
                    }

                    foreach (var moduleRegistration in GetEnabledModuleRegistrations(appContext))
                    {
                        moduleRegistration.ConfigureOptions(appContext, services);
                        moduleRegistration.ConfigureServices(appContext, services);
                    }
                }).ConfigureLogging((context, _) =>
                {
                    LogCheck("Configure logging");
                    var loggerConfiguration = new LoggerConfiguration();
                    loggerConfiguration.MinimumLevel.ControlledBy(logLevelSwitcher.Switch);
                    loggerConfiguration
                        .Enrich.FromLogContext()
                        .Enrich.WithMachineName()
                        .Enrich.WithProperty("App", Name)
                        .Enrich.WithProperty("AppVersion", Version);
                    logLevelSwitcher.Switch.MinimumLevel =
                        context.HostingEnvironment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information;

                    if (LoggingEnableConsole(context))
                    {
                        loggerConfiguration
                            .WriteTo.Console(
                                outputTemplate: ConsoleLogFormat,
                                levelSwitch: logLevelSwitcher.Switch);
                    }

                    var appContext = GetContext(context.HostingEnvironment, context.Configuration);
                    ConfigureLogging(appContext,
                        loggerConfiguration,
                        logLevelSwitcher);
                    foreach (var (key, value) in
                        ____RULE_VIOLATION____LogEventLevels____RULE_VIOLATION____)
                    {
                        loggerConfiguration.MinimumLevel.Override(key, value);
                    }

                    foreach (var moduleRegistration in GetEnabledModuleRegistrations(appContext))
                    {
                        moduleRegistration.ConfigureLogging(tmpApplicationContext, loggerConfiguration,
                            logLevelSwitcher);
                    }

                    foreach (var loggerConfigurationAction in loggerConfigurationActions)
                    {
                        loggerConfigurationAction(appContext, loggerConfiguration, logLevelSwitcher);
                    }

                    Log.Logger = loggerConfiguration.CreateLogger();
                });

            LogCheck("Configure host builder in modules");
            foreach (var moduleRegistration in GetEnabledModuleRegistrations(tmpApplicationContext))
            {
                moduleRegistration.ConfigureHostBuilder(tmpApplicationContext, hostBuilder);
            }

            LogCheck("Configure host builder");
            configure?.Invoke(hostBuilder);
            LogCheck("Create host builder done");
            return hostBuilder;
        }

        protected IHost CreateAppHost(Action<IHostBuilder>? configure = null)
        {
            LogCheck("Create app host start");

            if (appHost is not null)
            {
                LogCheck("App host is already built");

                return appHost;
            }

            LogCheck("Configure host builder");

            var hostBuilder = ConfigureHostBuilder(configure);

            LogCheck("Build host");
            var host = hostBuilder.Build();

            appHost = host;
            LogCheck("Create app host done");
            return appHost;
        }

        private IHostBuilder CreateHostBuilder(string[] hostBuilderArgs)
        {
            var builder = Host.CreateDefaultBuilder(hostBuilderArgs);
            ConfigureHostBuilder(builder);
            return builder;
        }

        protected virtual void ConfigureHostBuilder(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(ConfigureHostConfiguration);
            builder.ConfigureAppConfiguration(ConfigureAppConfiguration);
        }

        protected virtual void ConfigureHostConfiguration(IConfigurationBuilder configurationBuilder)
        {
        }

        protected virtual void ConfigureAppConfiguration(HostBuilderContext context,
            IConfigurationBuilder configurationBuilder)
        {
        }

        protected virtual bool LoggingEnableConsole(HostBuilderContext context) =>
            context.HostingEnvironment.IsDevelopment();

        protected virtual void ConfigureLogging(ApplicationContext applicationContext,
            LoggerConfiguration loggerConfiguration,
            LogLevelSwitcher appLogLevelSwitcher)
        {
        }

        public async Task RunAsync()
        {
            LogCheck("Run app start");
            LogCheck("Build and init");
            var host = await BuildAndInitAsync();

            InternalLogger.LogInformation("Check required modules");
            var context = GetContext(host.Services);
            var modulesCheckSuccess = true;
            foreach (var registration in GetEnabledModuleRegistrations(context))
            {
                var result =
                    registration.CheckRequiredModules(context,
                        GetEnabledModuleRegistrations(context).Select(r => r.Type).ToArray());
                if (!result.isSuccess)
                {
                    foreach (var missingModule in result.missingModules)
                    {
                        InternalLogger.LogCritical(
                            "Required module {MissingModule} for module {Module} is not registered",
                            missingModule, registration.Type);
                    }

                    modulesCheckSuccess = false;
                }
            }

            if (!modulesCheckSuccess)
            {
                InternalLogger.LogError("Check required modules failed");
                return;
            }

            if (____RULE_VIOLATION____IsCheckRun____RULE_VIOLATION____)
            {
                LogCheck("Check run is successful. Exit");
                return;
            }

            await host.RunAsync();
        }

        public async Task<IHost> StartAsync()
        {
            var host = await BuildAndInitAsync();

            await host.StartAsync();
            return host;
        }

        public async Task StopAsync() => await CreateAppHost().StopAsync();

        public async Task ExecuteAsync(Func<IServiceProvider, Task> command)
        {
            var host = await BuildAndInitAsync(builder => builder.UseConsoleLifetime());

            var serviceProvider = host.Services;

            try
            {
                using var scope = serviceProvider.CreateScope();
                await command(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetService<ILogger<Application>>();
                logger.LogError(ex, "Error: {ErrorText}", ex.ToString());
            }
        }

        public IHostBuilder GetHostBuilder() => ConfigureHostBuilder();

        public IServiceProvider GetServiceProvider() => CreateAppHost().Services;

        public T? GetService<T>() => GetServiceProvider().GetService<T>();


        protected void RegisterModule<TModule, TModuleOptions>(
            Action<IConfiguration, IHostEnvironment, TModuleOptions>? configureOptions = null,
            string? optionsKey = null)
            where TModule : IApplicationModule<TModuleOptions>, new() where TModuleOptions : BaseModuleOptions, new()
        {
            if (moduleRegistrations.ContainsKey(typeof(TModule)))
            {
                throw new Exception($"Module {typeof(TModule)} already registered");
            }

            moduleRegistrations.Add(typeof(TModule),
                new ApplicationModuleRegistration<TModule, TModuleOptions>(configureOptions, optionsKey));
        }

        protected virtual string? GetName(IHostEnvironment environment, IConfiguration configuration) => null;

        protected virtual string? GetVersion(IHostEnvironment environment, IConfiguration configuration) =>
            GetType().Assembly.GetName().Version?.ToString();

        protected virtual void InitApplication()
        {
        }

        public async Task<IHost> BuildAndInitAsync(Action<IHostBuilder>? configure = null)
        {
            LogCheck("Build and init async start");

            var host = CreateAppHost(configure);

            if (!____RULE_VIOLATION____IsCheckRun____RULE_VIOLATION____)
            {
                using var scope = host.Services.CreateScope();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Application>>();
                logger.LogInformation("Init modules");
                foreach (var module in GetEnabledModuleRegistrations(GetContext(scope.ServiceProvider)))
                {
                    logger.LogInformation("Init module {Module}", module.Type);
                    await module.InitAsync(
                        GetContext(scope.ServiceProvider), scope.ServiceProvider);
                }
            }

            LogCheck("Build and init async done");

            return host;
        }

        protected ApplicationContext GetContext(IServiceProvider serviceProvider) =>
            GetContext(serviceProvider.GetRequiredService<IHostEnvironment>(),
                serviceProvider.GetRequiredService<IConfiguration>(),
                serviceProvider.GetRequiredService<ILogger<Application>>());

        protected ApplicationContext GetContext(IHostEnvironment environment, IConfiguration configuration,
            ILogger<Application>? logger = null) =>
            new(Name, Version, environment, configuration, logger ?? InternalLogger);

        public async Task OnStarted(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Application>>();
            await OnStartedAsync(configuration, environment, serviceProvider);
            foreach (var moduleRegistration in GetEnabledModuleRegistrations(GetContext(serviceProvider)))
            {
                try
                {
                    await moduleRegistration.ApplicationStarted(configuration, environment, serviceProvider);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error on application started hook in module {Module}: {ErrorText}",
                        moduleRegistration.Type,
                        ex.ToString());
                }
            }
        }

        protected virtual Task OnStartedAsync(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider) =>
            Task.CompletedTask;

        public async Task OnStopping(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Application>>();
            await OnStoppingAsync(configuration, environment, serviceProvider);
            foreach (var moduleRegistration in GetEnabledModuleRegistrations(GetContext(serviceProvider)))
            {
                try
                {
                    await moduleRegistration.ApplicationStopping(configuration, environment, serviceProvider);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error on application stopping hook in module {Module}: {ErrorText}",
                        moduleRegistration.Type,
                        ex.ToString());
                }
            }
        }

        protected virtual Task OnStoppingAsync(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider) =>
            Task.CompletedTask;

        public async Task OnStopped(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider)
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Application>>();
            await OnStoppedAsync(configuration, environment, serviceProvider);
            foreach (var moduleRegistration in GetEnabledModuleRegistrations(GetContext(serviceProvider)))
            {
                try
                {
                    await moduleRegistration.ApplicationStopped(configuration, environment, serviceProvider);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error on application stopped hook in module {Module}: {ErrorText}",
                        moduleRegistration.Type,
                        ex.ToString());
                }
            }
        }

        protected virtual Task OnStoppedAsync(IConfiguration configuration, IHostEnvironment environment,
            IServiceProvider serviceProvider) =>
            Task.CompletedTask;

        public bool HasModule<TModule>() where TModule : IApplicationModule =>
            moduleRegistrations.ContainsKey(typeof(TModule));


        public void Set(string key, object value) => store[key] = value;

        public T Get<T>(string key)
        {
            if (store.ContainsKey(key))
            {
                return (T)store[key];
            }

#pragma warning disable 8603
            return default;
#pragma warning restore 8603
        }

        public Application ConfigureLogLevel(string source, LogEventLevel level)
        {
            ____RULE_VIOLATION____LogEventLevels____RULE_VIOLATION____.Add(source, level);
            return this;
        }

        public Application ConfigureLogging(Action<ApplicationContext, LoggerConfiguration, LogLevelSwitcher> configure)
        {
            loggerConfigurationActions.Add(configure);
            return this;
        }

        public Application ConfigureServices(
            Action<ApplicationContext, HostBuilderContext, IServiceCollection> configure)
        {
            servicesConfigurationActions.Add(configure);
            return this;
        }

        public Application ConfigureServices(Action<IServiceCollection> configure)
        {
            servicesConfigurationActions.Add((_, _, services) =>
            {
                configure(services);
            });
            return this;
        }

        public Application ConfigureAppConfiguration(
            Action<ApplicationContext, HostBuilderContext, IConfigurationBuilder> configure)
        {
            appConfigurationActions.Add(configure);
            return this;
        }

        public Application AddModule<TModule>() where TModule : BaseApplicationModule, new()

        {
            RegisterModule<TModule, BaseApplicationModuleOptions>();
            return this;
        }

        public Application AddModule<TModule, TModuleOptions>(
            Action<IConfiguration, IHostEnvironment, TModuleOptions> configureOptions,
            string? optionsKey = null)
            where TModule : IApplicationModule<TModuleOptions>, new()
            where TModuleOptions : BaseModuleOptions, new()
        {
            RegisterModule<TModule, TModuleOptions>(configureOptions, optionsKey);
            return this;
        }

        public Application AddModule<TModule, TModuleOptions>(
            Action<TModuleOptions>? configureOptions = null,
            string? optionsKey = null)
            where TModule : IApplicationModule<TModuleOptions>, new()
            where TModuleOptions : BaseModuleOptions, new() =>
            AddModule<TModule, TModuleOptions>((_, _, moduleOptions) =>
            {
                configureOptions?.Invoke(moduleOptions);
            }, optionsKey);
    }

    public class ApplicationContext
    {
        public ApplicationContext(string name, string version, IHostEnvironment environment,
            IConfiguration configuration, ILogger logger)
        {
            Name = name;
            Version = version;
            Environment = environment;
            Configuration = configuration;
            Logger = logger;
        }

        public string Name { get; }
        public string Version { get; }
        public IHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }
        public ILogger Logger { get; }
    }
}
