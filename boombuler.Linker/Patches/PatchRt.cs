namespace boombuler.Linker.Patches;

using System.Numerics;
using System.Runtime.InteropServices;

class PatchRt<TAddr> 
    where TAddr : INumberBase<TAddr>
{
    private readonly Func<SymbolId, TAddr> fResolveSymbol;
    public PatchRt(Func<SymbolId, TAddr> resolveSymbol)
    {
        ArgumentNullException.ThrowIfNull(resolveSymbol);

        fResolveSymbol = resolveSymbol;
    }

    public void Run(TAddr targetAddress, byte targetSize, ReadOnlySpan<byte> patches, Span<byte> target)
    {
        List<Expression> expressions = new List<Expression>();
        while (patches.Length > 0)
            expressions.Add(Expression.Read(ref patches));
        Run(targetAddress, targetSize, expressions, target);
    }

    public void Run(TAddr targetAddress, byte targetSize, IEnumerable<Expression> expressions, Span<byte> target)
    {
        var state = new Stack<ulong>();

        if (target.Length < targetSize)
            throw new ArgumentException($"Target buffer must be at least {targetSize} bytes long", nameof(target));

        foreach(var patch in expressions)
            Apply(targetAddress, patch, state);

        if (state.Count != 1)
            throw new InvalidOperationException($"Invalid Patch instructions. Stack contains {state.Count} items.");
        WriteResult(targetSize, state.Peek(), target);
    }


    protected virtual void WriteResult(byte targetSize, ulong value, Span<byte> target)
    {
        switch (targetSize)
        {
            case 1:
                MemoryMarshal.Write(target, (byte)value);
                break;
            case 2:
                MemoryMarshal.Write(target, (ushort)value);
                break;
            case 4:
                MemoryMarshal.Write(target, (uint)value);
                break;
            case 8:
                MemoryMarshal.Write(target, value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(targetSize), $"Target size {targetSize} is not supported");
        }
    }

    private void Apply(TAddr targetAddress, Expression express, Stack<ulong> state)
    {
        void UnaryOp(Func<ulong, ulong> op)
        {
            var a = state.Pop();
            state.Push(op(a));
        }
        void BinaryOp(Func<ulong, ulong, ulong> op)
        {
            var b = state.Pop();
            var a = state.Pop();
            state.Push(op(a, b));
        }

        switch (express.Type)
        {
            case ExpressionType.Push: 
                state.Push(express.Value); 
                break;
            case ExpressionType.Add:
                BinaryOp((a, b) => a + b);
                break;
            case ExpressionType.Sub:
                BinaryOp((a, b) => a - b);
                break;
            case ExpressionType.Mul:
                BinaryOp((a, b) => a * b);
                break;
            case ExpressionType.And:
                BinaryOp((a, b) => a & b);
                break;
            case ExpressionType.Or:
                BinaryOp((a, b) => a | b);
                break;
            case ExpressionType.Xor:
                BinaryOp((a, b) => a ^ b);
                break;
            case ExpressionType.Cpl:
                UnaryOp(a => ~a);
                break;
            case ExpressionType.Shl:
                BinaryOp((a, b) => a << (int)b);
                break;
            case ExpressionType.Shr:
                BinaryOp((a, b) => a >> (int)b);
                break;
            case ExpressionType.CurrentAdress:
                state.Push(ulong.CreateTruncating(targetAddress)); break;
            case ExpressionType.SymbolAdress:
                var symbol = new SymbolId(int.CreateTruncating(state.Pop()));
                state.Push(ulong.CreateTruncating(fResolveSymbol(symbol)));
                break;
            default:
                throw new NotImplementedException($"Expression type {express.Type} is not implemented");
        }
    }
}
