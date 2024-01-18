namespace CacheFlow.Settings;

public sealed class CacheSettings
{
    public TimeSpan ExpireTime { get; set; } = TimeSpan.FromMinutes(20);
}