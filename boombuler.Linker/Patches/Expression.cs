namespace boombuler.Linker.Patches;

using System.Diagnostics;

public enum ExpressionType
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
public readonly struct Expression
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