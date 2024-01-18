using Microsoft.Extensions.DependencyInjection;

namespace CacheFlow.Builders;

public sealed class CacheServiceBuilder
{
    public TimeSpan ExpireTime { get; set; } = TimeSpan.FromMinutes(20);

    public string ConnectionString { get; set; } = "localhost:6379";

    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;
}