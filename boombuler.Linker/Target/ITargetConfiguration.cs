namespace boombuler.Linker.Target;

using System.Numerics;

public interface ITargetConfiguration<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>
{
    IEnumerable<Anchor<TAddr>> Anchors { get; }

    IEnumerable<Region<TAddr>> Regions { get; }
}
