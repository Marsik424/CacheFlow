using CacheFlow.Builders;
using CacheFlow.Helpers;
using CacheFlow.Options;
using CacheFlow.Services;
using Metalama.Extensions.DependencyInjection;
using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;
using Newtonsoft.Json;

namespace CacheFlow.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class CacheAttribute : OverrideMethodAspect, IAspect<INamedType>
{
    [IntroduceDependency]
    private readonly ICacheService _cacheService = null!;

    public override async Task<dynamic?> OverrideAsyncMethod()
    {
        var cacheOptions = meta.Target.Method.DeclaringAssembly.GlobalNamespace.Enhancements().GetOptions<CacheOptionsAttribute>();
        if (!cacheOptions.UseRepositoryInterception)
        {
            return await meta.ProceedAsync();
        }
        else
        {
            string key = CacheKeyBuilder.GetCachingKey().ToValue();
            var taskResultType = meta.Target.Method.ReturnType.GetAsyncInfo().ResultType;

            var resultType = taskResultType.ToType();
            string hashKey = TypeAnalyzer.IsEnumerableType(taskResultType)
                ? resultType.GetGenericArguments()[0].Name
                : resultType.Name;
        
            dynamic? result = cacheOptions.UseReferenceCacheInvalidation ?
                !TypeAnalyzer.IsEnumerableType(taskResultType)
                    ? await _cacheService.HashScan(hashKey, $"*{key}*", resultType)
                    : await _cacheService.HashGetAsync(hashKey, "all", resultType)
                : await _cacheService.HashGetAsync(hashKey, key, resultType);
        
            if (result is not null)
            {
                return result;
            }

            dynamic? methodResponse = await meta.ProceedAsync();
            if (methodResponse is null)
            {
                return methodResponse;
            }
        
            if (!TypeAnalyzer.IsEnumerableType(taskResultType))
            {
                var stringBuilder = new InterpolatedStringBuilder();
                BuildComplexKey(stringBuilder,key, methodResponse, taskResultType);

                string complexKey = stringBuilder.ToValue();
                await _cacheService.HashSetAsync(hashKey, complexKey, JsonConvert.SerializeObject(methodResponse));
            
                return methodResponse;
            }

            await _cacheService.HashSetAsync(hashKey, key, JsonConvert.SerializeObject(methodResponse));
            return methodResponse;
        }
    }

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
    
    [Template]
    private void BuildComplexKey(InterpolatedStringBuilder stringBuilder, string key, dynamic methodResponse, IType taskResultType)
    {
        var returnProperties = TypeAnalyzer.GetReturnPropertiesFromReferencedAssemblies(taskResultType)
            ?? TypeAnalyzer.GetReturnProperties(taskResultType);
        stringBuilder.AddExpression(key);

        foreach (var property in returnProperties)
        {
            AppendPropertyToKey(stringBuilder, methodResponse, property);
        }
    }
    
    [Template]
    private void AppendPropertyToKey(InterpolatedStringBuilder stringBuilder, dynamic methodResponse, IProperty property)
    {
        var cacheOptions = meta.Target.Method.DeclaringAssembly.GlobalNamespace.Enhancements().GetOptions<CacheOptionsAttribute>();
        if (property.Name.IndexOf("Id", StringComparison.Ordinal) > 0)
        {
            AppendIdFromExplicitProperty(stringBuilder, methodResponse, property);
        }
        else if (TypeAnalyzer.IsEnumerableType(property.Type) && !property.Type.Is(SpecialType.String))
        {
            AppendArrayKey(stringBuilder, property);
            if (cacheOptions.UseReferenceCacheInvalidation)
            {
                GenerateCacheValueForArrayType(methodResponse, property);
            }
        }
        else if (property.Type.IsReferenceType is true && property.Type.TypeKind != TypeKind.Array && !property.Type.Is(SpecialType.String))
        {
            AppendIdFromReferenceProperty(stringBuilder, methodResponse, property);
            if (cacheOptions.UseReferenceCacheInvalidation)
            {
                GenerateCacheValueForReferenceProperty(methodResponse, property);
            }
        }
    }

    [Template]
    private void AppendIdFromExplicitProperty(InterpolatedStringBuilder stringBuilder, dynamic methodResponse, IProperty property)
    {
        stringBuilder.AddText("-");
        var expressionBuilder = new ExpressionBuilder();
        
        expressionBuilder.AppendExpression(methodResponse);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);

        stringBuilder.AddExpression(expressionBuilder.ToValue());
    }
    
    [Template]
    private static void AppendArrayKey(InterpolatedStringBuilder stringBuilder, IProperty property)
    {
        stringBuilder.AddText("-");
        stringBuilder.AddExpression(property.Type.ToType().GetGenericArguments()[0].Name);
        stringBuilder.AddText("-all");
    }

    [Template]
    private static void AppendIdFromReferenceProperty(InterpolatedStringBuilder stringBuilder, dynamic methodResponse, IProperty property)
    {
        var properties = TypeAnalyzer.GetReturnPropertiesFromReferencedAssemblies(property.Type)
            ?? TypeAnalyzer.GetReturnProperties(property.Type);
        
        var idProperty = properties.FirstOrDefault(prop => prop.Name.Equals("Id"));
        if (idProperty is null)
        {
            return;
        }

        stringBuilder.AddText("-");
        var expressionBuilder = new ExpressionBuilder();
        
        expressionBuilder.AppendExpression(methodResponse);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim("Id");
        
        stringBuilder.AddExpression(expressionBuilder.ToValue());
    }


    [Template]
    private async void GenerateCacheValueForArrayType(dynamic methodResponse, IProperty property)
    {
        var expressionBuilder = new ExpressionBuilder();
        expressionBuilder.AppendExpression(methodResponse);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);

        dynamic? arrayProperty = expressionBuilder.ToValue();
        await _cacheService.HashSetAsync(property.Name[..^1], "all", JsonConvert.SerializeObject(arrayProperty));
    }

    [Template]
    private async void GenerateCacheValueForReferenceProperty(dynamic methodResponse, IProperty property)
    {
        var expressionBuilder = new ExpressionBuilder();
        expressionBuilder.AppendExpression(methodResponse);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim(property.Name);
        
        dynamic propertyValue = expressionBuilder.ToValue()!;
        var interpolatedStringBuilder = new InterpolatedStringBuilder();
        interpolatedStringBuilder.AddExpression(methodResponse.Id.ToString());
        interpolatedStringBuilder.AddText("-");
        
        expressionBuilder = new ExpressionBuilder();
        expressionBuilder.AppendExpression(propertyValue);
        expressionBuilder.AppendVerbatim(".");
        expressionBuilder.AppendVerbatim("Id");

        string propertyId = expressionBuilder.ToValue()!.ToString();
        BuildComplexKey(interpolatedStringBuilder, propertyId, propertyValue, property.Type);

        string complexKey = interpolatedStringBuilder.ToValue();
        
        await _cacheService.HashRemoveAllAsync(property.Name, $"*{propertyId}*").ConfigureAwait(false);
        await _cacheService.HashSetAsync(property.Name, complexKey, JsonConvert.SerializeObject(propertyValue));
    }

}