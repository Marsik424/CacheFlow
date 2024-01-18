using CacheFlow.Attributes;
using Metalama.Framework.Code;
using Metalama.Framework.Fabrics;

namespace CacheFlow.Fabrics;

internal sealed class CacheAttributeProjectFabric : TransitiveProjectFabric
{
    public override void AmendProject(IProjectAmender amender)
    {
        amender.Outbound.SelectMany(compilation => compilation.GlobalNamespace
                .DescendantsAndSelf()
                .Where(ns => ns.FullName.EndsWith("Repositories")))
            .SelectMany(ns => ns.Types.SelectMany(type => type.Methods))
            .Where(method => method.HasImplementation && method.Name != "ToString" && method.Name.StartsWith("Get"))
            .AddAspectIfEligible<CacheAttribute>();

        amender.Outbound.SelectMany(compilation => compilation.GlobalNamespace
                .DescendantsAndSelf()
                .Where(ns => ns.FullName.EndsWith("Repositories")))
            .SelectMany(ns => ns.Types.SelectMany(type => type.Methods))
            .Where(method => method.HasImplementation && method.Name != "ToString")
            .Where(method => method.Name.StartsWith("Create") || method.Name.StartsWith("Update") || method.Name.StartsWith("Delete"))
            .AddAspectIfEligible<CacheInvalidationAttribute>();
    }
}