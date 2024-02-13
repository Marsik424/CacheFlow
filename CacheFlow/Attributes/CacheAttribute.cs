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
public class CacheAttribute : OverrideMethodAspect, IAspect<INamedType>
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

        string key = CacheKeyBuilder.GetCachingKey().ToValue();
        var taskResultType = meta.Target.Method.ReturnType.GetAsyncInfo().ResultType;

        var resultType = taskResultType.ToType();
        string hashKey = TypeAnalyzer.IsEnumerableType(taskResultType)
            ? resultType.GetGenericArguments()[0].Name
            : resultType.Name;

        dynamic? result = await _cacheService.HashGetAsync(hashKey, key, resultType);
        if (result is not null)
        {
            return result;
        }

        dynamic? methodResponse = await meta.ProceedAsync();
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

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
    
    [Template]
    private void BuildComplexKey(InterpolatedStringBuilder stringBuilder ,string key, dynamic methodResponse, IType taskResultType)
    {
        var returnProperties = TypeAnalyzer.GetReturnProperties(taskResultType);
        stringBuilder.AddExpression(key);

        foreach (var property in returnProperties)
        {
            AppendPropertyToKey(stringBuilder, methodResponse, property);
        }
    }
    
    [Template]
    private static void AppendPropertyToKey(InterpolatedStringBuilder stringBuilder, dynamic methodResponse, IProperty property)
    {
        if (property.Name.IndexOf("Id", StringComparison.Ordinal) > 0)
        {
            AppendIdFromExplicitProperty(stringBuilder, methodResponse, property);
        }
        else if (TypeAnalyzer.IsEnumerableType(property.Type) && !property.Type.Is(SpecialType.String))
        {
            AppendArrayKey(stringBuilder, property);
        }
        else if (property.Type.IsReferenceType is true && !property.Name.Equals("Id") )
        {
            AppendIdFromReferenceProperty(stringBuilder, methodResponse, property);
        }
    }

    [Template]
    private static void AppendIdFromExplicitProperty(InterpolatedStringBuilder stringBuilder, dynamic methodResponse, IProperty property)
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
        var properties = TypeAnalyzer.GetReturnProperties(property.Type);
        var idProperty = properties.FirstOrDefault(prop => prop.Name.Equals("Id"));
        if (idProperty is not null)
        {
            stringBuilder.AddText("-");
            var expressionBuilder = new ExpressionBuilder();
        
            expressionBuilder.AppendExpression(methodResponse);
            expressionBuilder.AppendVerbatim(".");
            expressionBuilder.AppendVerbatim(property.Name);
            expressionBuilder.AppendVerbatim(".");
            expressionBuilder.AppendVerbatim("Id");
        
            stringBuilder.AddExpression(expressionBuilder.ToValue());   
        }
    }

}