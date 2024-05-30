using CacheFlow.Helpers;
using CacheFlow.Options;
using CacheFlow.Services;
using Metalama.Extensions.DependencyInjection;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;

namespace CacheFlow.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CacheInvalidationAttribute : OverrideMethodAspect, IAspect<INamedType>
{
    [IntroduceDependency]
    private readonly ICacheService _cacheService = null!;

    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        dynamic? methodResponse = await meta.ProceedAsync();

        var cacheOptions = meta.Target.Method.DeclaringAssembly.GlobalNamespace.Enhancements().GetOptions<CacheOptionsAttribute>();
        if (cacheOptions.UseRepositoryInterception)
        {
            HandleRepositoryCacheInvalidation(cacheOptions.UseReferenceCacheInvalidation);
        }
        
        return methodResponse;
    }
    
    [Template]
    private void HandleRepositoryCacheInvalidation(bool useReferenceHandler)
    {
        int repositoryIndex = meta.Target.Method.DeclaringType.Name.IndexOf("Repository", StringComparison.Ordinal);
        var parameter = meta.Target.Method.Parameters[0];
        string hashKey = meta.Target.Method.DeclaringType.Name[..repositoryIndex];
        
        if (parameter.Type.Is(SpecialType.String) || parameter.Type.Is(typeof(Guid)))
        {
            if (useReferenceHandler)
            {
                InvalidateCacheUsingReferenceHandler(hashKey, parameter);
            }
            else
            {
                InvalidateCacheForSimpleParameterAsync(hashKey, parameter);
            }
        }
        else if (parameter.Type.IsReferenceType is true)
        {
            if (useReferenceHandler)
            {
                InvalidateCacheUsingReferenceHandler(hashKey, parameter, useReferenceTypeFromParameter: true);
            }
            else
            {
                InvalidateCacheForReferenceTypeAsync(hashKey, parameter);
            }
        }
    }
    
    [Template]
    private async void InvalidateCacheForSimpleParameterAsync([CompileTime] string hashKey, IParameter parameter)
    {
        await _cacheService.HashRemoveAsync(hashKey, parameter.Value!.ToString()).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
    }

    [Template]
    private async void InvalidateCacheUsingReferenceHandler([CompileTime] string hashKey, IParameter parameter, [CompileTime] bool useReferenceTypeFromParameter = false)
    {        
        var referenceProperties = useReferenceTypeFromParameter
            ? TypeAnalyzer.GetReturnPropertiesFromReferencedAssemblies(parameter.Type) ?? TypeAnalyzer.GetReturnProperties(parameter.Type)
            : TypeAnalyzer.GetReturnPropertiesFromReferencedAssemblies(hashKey) ?? TypeAnalyzer.GetReturnProperties(hashKey);

        
        string propertyKeyPattern = useReferenceTypeFromParameter ? $"*{parameter.Value!.Id}*" : $"*{parameter.Value}*";
        string arrayKeyPattern = $"*{hashKey}*";
        
        referenceProperties = referenceProperties.Where(property =>
            property.Type.IsReferenceType is true && !property.Type.Is(SpecialType.String) && !property.Type.Is(typeof(Guid)));
        
        foreach (var property in referenceProperties)
        {
            string propertyHashKey = TypeAnalyzer.IsEnumerableType(property.Type)
                ? property.Type.ToType().GetGenericArguments()[0].Name
                : property.Name;

            if (property.Type.Is(typeof(Guid)) && propertyHashKey.IndexOf("Id", StringComparison.Ordinal) is var indexOf)
            {
                propertyHashKey = propertyHashKey[..indexOf];
            }
            
            await _cacheService.HashRemoveAllAsync(propertyHashKey, propertyKeyPattern).ConfigureAwait(false);
            await _cacheService.HashRemoveAllAsync(propertyHashKey, arrayKeyPattern).ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(propertyHashKey, "all").ConfigureAwait(false);
        }
        
        await _cacheService.HashRemoveAllAsync(hashKey, propertyKeyPattern).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
    }
    
    [Template]
    private async void InvalidateCacheForReferenceTypeAsync(string hashKey, IParameter parameter)
    {
        var parameterProperties = TypeAnalyzer.GetReturnPropertiesFromReferencedAssemblies(parameter.Type)
                                  ?? TypeAnalyzer.GetReturnProperties(parameter.Type);
        
        string parameterId = parameter.Value!.Id.ToString();
        foreach (var property in parameterProperties)
        {
            HandlePropertyCacheInvalidationAsync(parameter, property);
        }

        await _cacheService.HashRemoveAsync(hashKey, parameterId).ConfigureAwait(false);
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
                 && !TypeAnalyzer.IsEnumerableType(property.Type)
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

        string id = expressionBuilder.ToValue()!.ToString();
        int indexOfId = property.Name.IndexOf("Id", StringComparison.Ordinal);
        
        await _cacheService.HashRemoveAsync(property.Name[..indexOfId], id).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(property.Name[..indexOfId], "all").ConfigureAwait(false);
    }

    [Template]
    private async void InvalidateCacheForReferencePropertyAsync(ExpressionBuilder expressionBuilder, IParameter parameter,
        IProperty property)
    {
        var idProperty = property.DeclaringAssembly.Types
            .FirstOrDefault(type => type.FullName.Contains(property.Name))
            ?.Properties
            .FirstOrDefault(prop => prop.Name.Contains("Id"));

        if (idProperty != null)
        {
            expressionBuilder.AppendVerbatim(parameter.Name);
            expressionBuilder.AppendVerbatim(".");
            expressionBuilder.AppendVerbatim(property.Name);
            expressionBuilder.AppendVerbatim(".");
            expressionBuilder.AppendVerbatim(idProperty.Name);

            string id = expressionBuilder.ToValue()!.ToString();
            await _cacheService.HashRemoveAsync(property.Name, id).ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(property.Name, "all").ConfigureAwait(false);
        }
    }

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
}