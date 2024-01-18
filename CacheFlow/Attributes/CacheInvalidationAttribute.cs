using CacheFlow.Options;
using CacheFlow.Services;
using Metalama.Extensions.DependencyInjection;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;

namespace CacheFlow.Attributes;

[AttributeUsage(AttributeTargets.Method)]
[Inheritable]
public sealed class CacheInvalidationAttribute : OverrideMethodAspect, IAspect<INamedType>
{
    [IntroduceDependency]
    private readonly ICacheService _cacheService = null!;

    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        dynamic? methodResponse = await meta.ProceedAsync();

        var cacheOptions = meta.Target.Method.DeclaringAssembly.GlobalNamespace.Enhancements().GetOptions<CacheOptionsAttribute>();
        if (!cacheOptions.UseRepositoryInterception)
        {
            return methodResponse;
        }

        HandleRepositoryCacheInvalidation();
        return methodResponse;
    }

    [Template]
    private void HandleRepositoryCacheInvalidation()
    {
        int repositoryIndex = meta.Target.Method.DeclaringType.Name.IndexOf("Repository", StringComparison.Ordinal);
        var parameter = meta.Target.Method.Parameters[0];
        string hashKey = meta.Target.Method.DeclaringType.Name[..repositoryIndex];

        if (parameter.Type.Is(SpecialType.String) || parameter.Type.Is(typeof(Guid)))
        {
            InvalidateCacheForSimpleParameterAsync(hashKey, parameter);
        }
        else if (parameter.Type.IsReferenceType is true)
        {
            InvalidateCacheForReferenceTypeAsync(hashKey, parameter);
        }
    }

    [Template]
    private async void InvalidateCacheForSimpleParameterAsync(string hashKey, IParameter parameter)
    {
        await _cacheService.HashRemoveAsync(hashKey, parameter.Value!.ToString()).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
    }

    [Template]
    private async void InvalidateCacheForReferenceTypeAsync(string hashKey, IParameter parameter)
    {
        var parameterProperties = parameter.DeclaringAssembly.ReferencedAssemblies
            .FirstOrDefault(assembly => assembly.Types.Contains(parameter.Type))
            ?.Types
            .SelectMany(type => type.Properties)
            .Where(property => property.DeclaringType.Is(parameter.Type));


        if (parameterProperties is not null)
        {
            foreach (var property in parameterProperties)
            {
                HandlePropertyCacheInvalidationAsync(parameter, property);
            }
        }

        await _cacheService.HashRemoveAsync(hashKey, parameter.Value!.Id.ToString()).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
    }

    [Template]
    private void HandlePropertyCacheInvalidationAsync(IParameter parameter, IProperty property)
    {
        var expressionBuilder = new ExpressionBuilder();
        if (property.Name.IndexOf("Id", StringComparison.Ordinal) > 0)
        {
            InvalidateCacheForIdPropertyAsync(expressionBuilder, parameter, property);
        }
        else if (property.Type.IsReferenceType is true
            && !property.Type.Is(SpecialType.IEnumerable_T, ConversionKind.TypeDefinition)
            && property.Type.TypeKind != TypeKind.Array)
        {
            InvalidateCacheForReferencePropertyAsync(expressionBuilder, parameter, property);
        }
    }

    [Template]
    private async void InvalidateCacheForIdPropertyAsync(ExpressionBuilder expressionBuilder, IParameter parameter,
        IProperty property)
    {
        expressionBuilder.AppendVerbatim(parameter.Name);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);

        dynamic? id = expressionBuilder.ToValue()?.ToString();
        int indexOfId = property.Name.IndexOf("Id", StringComparison.Ordinal);

        await _cacheService.HashRemoveAsync(property.Name[..indexOfId], id).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(property.Name[..indexOfId], "all").ConfigureAwait(false);
    }

    [Template]
    private async void InvalidateCacheForReferencePropertyAsync(ExpressionBuilder expressionBuilder, IParameter parameter,
        IProperty property)
    {
        var idProperty = property.Compilation.Types
            .SelectMany(type => type.Properties)
            .FirstOrDefault(prop => prop.Name.Equals("Id", StringComparison.Ordinal));

        if (idProperty is null)
        {
            return;
        }

        expressionBuilder.AppendVerbatim(parameter.Name);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(idProperty.Name);

        dynamic? id = expressionBuilder.ToValue()?.ToString();

        await _cacheService.HashRemoveAsync(property.Name, id).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(property.Name, "all").ConfigureAwait(false);
    }

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
}