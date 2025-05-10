namespace boombuler.Linker.Module;

public enum SymbolType
{
    Internal = 0,
    Exported = 1,
    Imported = 2,
}
public record Symbol(SymbolName Name, SymbolType Type);
