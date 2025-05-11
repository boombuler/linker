namespace boombuler.Linker;

public readonly record struct SymbolName(string Global, string? Local = null)
{
    public static SymbolName Internal = new (string.Empty);
}
