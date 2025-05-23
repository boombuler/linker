namespace boombuler.Linker.Patches;

using System.Numerics;

public class Patch<TAddr>
    where TAddr : INumberBase<TAddr>
{
    public TAddr Location { get; init; } = TAddr.Zero;
    public byte Size { get; init; }
    public IReadOnlyCollection<Expression> Expressions { get; init; } = Array.Empty<Expression>();
}
