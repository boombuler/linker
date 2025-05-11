namespace boombuler.Linker.Module;

public enum SymbolType
{
    Internal = 0,
    Exported = 1,
    Imported = 2,
}
public record Symbol
{
    public static readonly Symbol Internal = new(SymbolName.Internal, SymbolType.Internal);

    public SymbolName Name { get; }
    public SymbolType Type { get; }

    public Symbol(SymbolName name, SymbolType type)
    {
        Name = name;
        Type = type;
        if (type != SymbolType.Internal && string.IsNullOrEmpty(name.Global))
            throw new ArgumentException("Internal symbols must have a name");
    }
}
