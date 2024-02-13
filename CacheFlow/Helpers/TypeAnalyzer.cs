using Metalama.Framework.Aspects;
using Metalama.Framework.Code;

namespace CacheFlow.Helpers;

[CompileTime]
internal static class TypeAnalyzer
{
    [CompiledTemplate]
    internal static bool IsEnumerableType(IType type)
    {
        return type.Is(SpecialType.IEnumerable_T, ConversionKind.TypeDefinition);
    }
        
    [CompiledTemplate]
    internal static IEnumerable<IProperty> GetReturnProperties(IType type)
    {
        return type.Compilation
            .Types
            .SelectMany(namedType => namedType.Properties)
            .Where(property => property.DeclaringType.Is(type));
    }
    
    [CompiledTemplate]
    internal static IEnumerable<IProperty>? GetReturnPropertiesFromReferencedAssemblies(IType type)
    {
        return type.Compilation.DeclaringAssembly.ReferencedAssemblies
            .FirstOrDefault(assembly => assembly.Types.Contains(type))
            ?.Types
            .SelectMany(namedType => namedType.Properties)
            .Where(property => property.DeclaringType.Is(type));
    }
}