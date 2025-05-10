namespace boombuler.Linker;

public readonly record struct SymbolId
{
    public int Value { get; }
    public SymbolId(int value)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException();
        Value = value;
    }
}
