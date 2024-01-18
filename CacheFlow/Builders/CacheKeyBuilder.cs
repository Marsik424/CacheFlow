using Metalama.Framework.Aspects;
using Metalama.Framework.Code;
using Metalama.Framework.Code.SyntaxBuilders;

namespace CacheFlow.Builders;

[CompileTime]
internal static class CacheKeyBuilder
{
    public static InterpolatedStringBuilder GetCachingKey()
    {
        var stringBuilder = new InterpolatedStringBuilder();
        
        var parameter = meta.Target.Parameters.FirstOrDefault();
        parameter = parameter is null || parameter.Type.Is(typeof(CancellationToken))
            ? null
            : parameter;
        
        stringBuilder.AddExpression(parameter?.Value ?? "all");
        return stringBuilder;
    }
}