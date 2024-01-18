using CacheFlow.Builders;
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
        string hashKey = taskResultType.Is(SpecialType.IEnumerable_T, ConversionKind.TypeDefinition)
            ? resultType.GetGenericArguments()[0].Name
            : resultType.Name;

        dynamic? result = await _cacheService.HashGetAsync(hashKey, key, resultType);
        if (result is not null)
        {
            return result;
        }

        dynamic? methodResponse = await meta.ProceedAsync();
        await _cacheService.HashSetAsync(hashKey, key, JsonConvert.SerializeObject(methodResponse));

        return methodResponse;
    }

    public override dynamic? OverrideMethod()
    {
        return meta.Proceed();
    }
}