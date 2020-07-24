using System;
using Grpc.AspNetCore.Server;

namespace Sitko.Core.Grpc.Server
{
    public class GrpcServerOptions
    {
        public string? Host { get; set; }
        public int? Port { get; set; }

        public bool UseTls { get; set; } = true;
        public bool ValidateTls { get; set; } = true;
        public TimeSpan ChecksInterval { get; set; } = TimeSpan.FromSeconds(60);
        public TimeSpan DeregisterTimeout { get; set; } = TimeSpan.FromSeconds(60);

        public bool EnableReflection { get; set; } = false;
        public bool EnableDetailedErrors { get; set; } = false;
        public Action<GrpcServiceOptions>? Configure { get; set; }

        public bool AutoFixRegistration { get; set; } = false;
    }
}
