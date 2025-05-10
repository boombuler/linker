namespace boombuler.Linker;

public readonly record struct RegionName
{
    public string Value { get; }

    public RegionName(string value)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
}