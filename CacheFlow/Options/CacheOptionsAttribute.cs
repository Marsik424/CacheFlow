using Metalama.Framework.Code;
using Metalama.Framework.Options;
using Metalama.Framework.Services;

namespace CacheFlow.Options;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class CacheOptionsAttribute : Attribute, IHierarchicalOptions<IMethod>, IProjectService, IHierarchicalOptions<IDeclaration>, IHierarchicalOptionsProvider
{
    public bool UseRepositoryInterception { get; init; } = true;

    public bool UseReferenceCacheInvalidation { get; init; }

    public IEnumerable<IHierarchicalOptions> GetOptions(in OptionsProviderContext context)
    {
        return new[]
        {
            new CacheOptionsAttribute
            {
                UseRepositoryInterception = UseRepositoryInterception,
                UseReferenceCacheInvalidation = UseReferenceCacheInvalidation
            }
        };
    }

    public object ApplyChanges(object changes, in ApplyChangesContext context)
    {
        var other = (CacheOptionsAttribute) changes;
        return new CacheOptionsAttribute
        {
            UseRepositoryInterception = other.UseRepositoryInterception,
            UseReferenceCacheInvalidation = other.UseReferenceCacheInvalidation
        };
    }
}