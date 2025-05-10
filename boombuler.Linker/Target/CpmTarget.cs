namespace boombuler.Linker.Target;

internal class CpmTarget : ITargetConfiguration<ushort>
{
    public IEnumerable<Anchor<ushort>> Anchors { get; } = [
        new Anchor<ushort>(new SymbolName("__MAIN__"), 0x0100),
    ];

    public IEnumerable<Region<ushort>> Regions { get; } = [
        new Region<ushort>(new RegionName(".text"), 0x0100),
        new Region<ushort>(new RegionName(".data")),
        new Region<ushort>(new RegionName(".bss"), Output: false),
    ];
}
