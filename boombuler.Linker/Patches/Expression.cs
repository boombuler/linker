namespace boombuler.Linker.Patches;

using System.Diagnostics;

enum ExpressionType
{
    Push = 0, // Push a single Immediate Value
    Add = 1,
    Sub = 2,
    Mul = 3,
    And = 4,
    Or  = 5,
    Xor = 6,
    Cpl = 7,
    Shl = 8,
    Shr = 9,
    CurrentAdress = 10,
    SymbolAdress = 11,
}

[DebuggerDisplay("{Type} {ValueDebuggerDisplay,nq}")]
readonly struct Expression
{
    public ExpressionType Type { get; init; }
    public ulong Value { get; init; }

    // Stryker disable all: ValueDebuggerDisplay is just used for debugger display. 
    private string ValueDebuggerDisplay
    {
        get
        {
            switch (Type)
            {
                case ExpressionType.Push:
                    return $"0x{Value:X8}";
                case ExpressionType.SymbolAdress:
                    return $"of {Value}";
                default:
                    return string.Empty;
            }
        }
    }
    // Stryker restore all

    public static Expression Read(ref ReadOnlySpan<byte> buffer)
    {
        ReadOnlySpan<byte> Read(ref ReadOnlySpan<byte>buffer, int count)
        {
            if (buffer.Length < count)
                throw new ArgumentException("Buffer is too small", nameof(buffer));
            var result = buffer[..count];
            buffer = buffer[count..];
            return result;
        }

        var op = Read(ref buffer, 1)[0];
        var type = (ExpressionType)(op & 0x0F);
        ulong arg = ((op & 0xF0) >> 4) switch {
            1 => Read(ref buffer, 1)[0],
            2 => BitConverter.ToUInt16(Read(ref buffer, 2)),
            4 => BitConverter.ToUInt32(Read(ref buffer, 4)),
            8 => BitConverter.ToUInt64(Read(ref buffer, 8)),
            _ => 0L,
        };
        return new Expression { Type = type, Value = arg };
    }

    public static Expression Add => new Expression { Type = ExpressionType.Add };
    public static Expression Sub => new Expression { Type = ExpressionType.Sub };
    public static Expression Mul => new Expression { Type = ExpressionType.Mul };
    public static Expression And => new Expression { Type = ExpressionType.And };
    public static Expression Or => new Expression { Type = ExpressionType.Or };
    public static Expression Xor => new Expression { Type = ExpressionType.Xor };
    public static Expression Cpl => new Expression { Type = ExpressionType.Cpl };
    public static Expression Shl => new Expression { Type = ExpressionType.Shl };
    public static Expression Shr => new Expression { Type = ExpressionType.Shr };
    public static Expression CurrentAdress => new Expression { Type = ExpressionType.CurrentAdress };
    public static Expression SymbolAdress(SymbolId id) => new Expression { Type = ExpressionType.SymbolAdress, Value = (ulong)id.Value };
    public static Expression Push(ulong value) => new Expression { Type = ExpressionType.Push, Value = value };
}