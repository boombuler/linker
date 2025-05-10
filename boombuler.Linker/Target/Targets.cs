namespace boombuler.Linker.Target;

public static class Targets
{
    public static ITargetConfiguration<ushort> CP_M => new CpmTarget();
}
