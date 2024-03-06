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
        if (!cacheOptions.UseRepositoryInterception)
        {
            return methodResponse;
        }
        
        HandleRepositoryCacheInvalidation(cacheOptions.UseReferenceCacheInvalidation);
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
            
            InvalidateCacheForSimpleParameterAsync(hashKey, parameter);
        }
        else if (parameter.Type.IsReferenceType is true)
        {
            InvalidateCacheForReferenceTypeAsync(hashKey, parameter);
        }
    }
    
    [Template]
    private async void InvalidateCacheForSimpleParameterAsync([CompileTime] string hashKey, IParameter parameter)
    {
        await _cacheService.HashRemoveAsync(hashKey, parameter.Value!.ToString()).ConfigureAwait(false);
        await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
    }

    [Template]
    private async void InvalidateCacheUsingReferenceHandler([CompileTime] string hashKey, IParameter parameter)
    {        
        var propertyToInvalidate = meta.Target.Method.Compilation.GlobalNamespace
            .DescendantsAndSelf()
            .Where(ns => ns.FullName.Contains("Models"))
            .SelectMany(ns => ns.Types)
            .FirstOrDefault(type => type.FullName.Contains(hashKey));

        if (propertyToInvalidate is null)
        {
            return;
        }

        var referenceProperties = propertyToInvalidate.Properties
            .Where(property => property.Type.IsReferenceType == true
                               && !property.Type.Is(SpecialType.String));

        string propertyKeyPattern = $"*{parameter.Value}*";
        string arrayKeyPattern = $"*{hashKey}*";
        
        foreach (var property in referenceProperties)
        {
            string propertyHashKey = TypeAnalyzer.IsEnumerableType(property.Type)
                ? property.Type.ToType().GetGenericArguments()[0].Name
                : property.Name;
            
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
        
        var cacheOptions = meta.Target.Method.DeclaringAssembly.GlobalNamespace.Enhancements().GetOptions<CacheOptionsAttribute>();
        foreach (var property in parameterProperties)
        {
            HandlePropertyCacheInvalidationAsync(parameter, property, cacheOptions.UseReferenceCacheInvalidation);
        }

        string parameterId = parameter.Value!.ToString();
        if (cacheOptions.UseReferenceCacheInvalidation)
        {
            await _cacheService.HashRemoveAllAsync(hashKey, $"*{parameterId}*").ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
        }
        else
        {
            await _cacheService.HashRemoveAsync(hashKey, parameterId).ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(hashKey, "all").ConfigureAwait(false);
        }
    }
    
    [Template]
    private void HandlePropertyCacheInvalidationAsync(IParameter parameter, IProperty property, bool useReferenceHandler = false)
    {
        var expressionBuilder = new ExpressionBuilder();
        
        if (property.Name.IndexOf("Id", StringComparison.Ordinal) > 0)
        {
            InvalidateCacheForIdPropertyAsync(expressionBuilder, parameter, property, useReferenceHandler);
        }
        else if (property.Type.IsReferenceType is true
                 && !TypeAnalyzer.IsEnumerableType(property.Type)
                 && property.Type.TypeKind != TypeKind.Array)
        {
            InvalidateCacheForReferencePropertyAsync(expressionBuilder, parameter, property, useReferenceHandler);
        }
    }

    [Template]
    private async void InvalidateCacheForIdPropertyAsync(ExpressionBuilder expressionBuilder, IParameter parameter, 
        IProperty property, bool useReferenceHandler = false)
    {
        expressionBuilder.AppendVerbatim(parameter.Name);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);

        string id = expressionBuilder.ToValue()!.ToString();
        int indexOfId = property.Name.IndexOf("Id", StringComparison.Ordinal);
        if (useReferenceHandler)
        {
            await _cacheService.HashRemoveAllAsync(property.Name[..indexOfId], $"*{id}*").ConfigureAwait(false);
            await _cacheService.HashRemoveAllAsync(property.Name[..indexOfId], $"*{parameter.Type}*").ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(property.Name[..indexOfId], "all").ConfigureAwait(false);
        }
        else
        {
            await _cacheService.HashRemoveAsync(property.Name[..indexOfId], id).ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(property.Name[..indexOfId], "all").ConfigureAwait(false);
        }
        
    }

    [Template]
    private async void InvalidateCacheForReferencePropertyAsync(ExpressionBuilder expressionBuilder, IParameter parameter,
        IProperty property, bool useReferenceHandler = false)
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

        string id = expressionBuilder.ToValue()!.ToString();
        if (useReferenceHandler)
        {
            await _cacheService.HashRemoveAllAsync(property.Name, $"*{id}*").ConfigureAwait(false);
            await _cacheService.HashRemoveAllAsync(property.Name, $"*{parameter.Type}*").ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(property.Name, "all").ConfigureAwait(false);
            
        }
        else
        {
            await _cacheService.HashRemoveAsync(property.Name, id).ConfigureAwait(false);
            await _cacheService.HashRemoveAsync(property.Name, "all").ConfigureAwait(false);
        }
        
    }

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
}