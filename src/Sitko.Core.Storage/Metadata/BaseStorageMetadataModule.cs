﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Sitko.Core.App;

namespace Sitko.Core.Storage.Metadata;

public abstract class
    BaseStorageMetadataModule<TStorageOptions, TProvider, TProviderOptions> : BaseApplicationModule<
        TProviderOptions> where TStorageOptions : StorageOptions
    where TProvider : class, IStorageMetadataProvider<TStorageOptions, TProviderOptions>
    where TProviderOptions : StorageMetadataModuleOptions<TStorageOptions>, new()
{
    public override void ConfigureServices(IApplicationContext context, IServiceCollection services,
        TProviderOptions startupOptions)
    {
        base.ConfigureServices(context, services, startupOptions);
        services.AddSingleton<IStorageMetadataProvider<TStorageOptions>, TProvider>();
        services.AddSingleton<TProvider>();
    }

    public override async Task InitAsync(IApplicationContext context, IServiceProvider serviceProvider)
    {
        await base.InitAsync(context, serviceProvider);
        var metadataProvider = serviceProvider.GetRequiredService<TProvider>();
        await metadataProvider.InitAsync();
    }

    public override IEnumerable<Type> GetRequiredModules(IApplicationContext context, TProviderOptions options) =>
        new[] { typeof(IStorageModule) };
}
