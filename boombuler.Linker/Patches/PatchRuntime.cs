namespace boombuler.Linker.Patches;

using System.Numerics;

public class PatchRuntime<TAddr> 
    where TAddr : INumberBase<TAddr>
{
    private readonly Func<SymbolId, TAddr> fResolveSymbol;
    public PatchRuntime(Func<SymbolId, TAddr> resolveSymbol)
    {
        ArgumentNullException.ThrowIfNull(resolveSymbol);

        fResolveSymbol = resolveSymbol;
    }

    public void Run(TAddr targetAddress, byte targetSize, IEnumerable<Expression> expressions, Stream target)
    {
        var state = new Stack<ulong>();

        foreach(var patch in expressions)
            Apply(targetAddress, patch, state);

        if (state.Count != 1)
            throw new InvalidOperationException($"Invalid Patch instructions. Stack contains {state.Count} items.");
        
        WriteResult(targetSize, state.Peek(), target);
    }

    protected virtual void WriteResult(byte targetSize, ulong value, Stream target)
    {
        while (targetSize-- > 0)
        {
            target.WriteByte((byte)value);
            value >>= 8;
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
                var symbol = new SymbolId((int)express.Value);
                state.Push(ulong.CreateTruncating(fResolveSymbol(symbol)));
                break;
            default:
                throw new NotImplementedException($"Expression type {express.Type} is not implemented");
        }
    }
}
