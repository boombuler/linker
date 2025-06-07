namespace boombuler.Linker.Patches;

/// <summary>
/// Helper class to build a list of expressions, optimizing constant calculations
/// while adding Expressions.
/// </summary>
public class ExpressionBuilder
{
    private readonly List<Expression> fExpressions = new();
    
    public ExpressionBuilder Add(Expression expression)
    {
        switch (expression.Type)
        {
            case ExpressionType.Add when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a + b);
                break;
            case ExpressionType.Sub when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a - b);
                break;
            case ExpressionType.Mul when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a * b);
                break;
            case ExpressionType.And when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a & b);
                break;
            case ExpressionType.Or when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a | b);
                break;
            case ExpressionType.Xor when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a ^ b);
                break;
            case ExpressionType.Shl when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a << (int)b);
                break;
            case ExpressionType.Shr when TryGetArgs(out var a, out var b):
                fExpressions.RemoveAt(fExpressions.Count - 1);
                fExpressions[^1] = Expression.Push(a >> (int)b);
                break;
            case ExpressionType.Cpl when TryGetArgs(out var arg):
                fExpressions[^1] = Expression.Push(~arg);
                break;
            default:
                fExpressions.Add(expression);
                break;
        }
        return this;
    }

    public ExpressionBuilder AddRange(IEnumerable<Expression> expressions)
    {
        foreach (var ex in expressions)
            Add(ex);
        return this;
    }

    private bool TryGetArgs(out ulong arg)
    {
        if (fExpressions.Count > 0 && fExpressions[^1] is { Type: ExpressionType.Push, Value: var value })
        {
            arg = value;
            return true;
        }
        arg = default;
        return false;
    }

    private bool TryGetArgs(out ulong a, out ulong b)
    {
        if (fExpressions.Count > 1 && 
            fExpressions[^1] is { Type: ExpressionType.Push, Value: var valueB } &&
            fExpressions[^2] is { Type: ExpressionType.Push, Value: var valueA })
        {
            a = valueA;
            b = valueB;
            return true;
        }
        a = b = default;
        return false;
    }

    public IReadOnlyCollection<Expression> Build()
    {
        if (fExpressions.Count == 0)
            return Array.Empty<Expression>();
        return fExpressions.ToArray();
    }
}
