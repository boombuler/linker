namespace boombuler.Linker.Module;

using System.Collections.Immutable;
using System.Numerics;

public class Module<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
{
    public string Name { get; init; } = string.Empty;
    public ImmutableArray<Symbol> Symbols { get; init; } = ImmutableArray<Symbol>.Empty;

    public ImmutableArray<Section<TAddr>> Sections { get; init; } = ImmutableArray<Section<TAddr>>.Empty;

    public Symbol GetSymbol(SymbolId id)
    {
        if (id.Value >= Symbols.Length)
            throw new ArgumentOutOfRangeException("Invalid Symbol-Id");
        return Symbols[id.Value];
    }
}
