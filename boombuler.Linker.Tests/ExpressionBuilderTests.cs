namespace boombuler.Linker.Tests;

using boombuler.Linker.Patches;
using static boombuler.Linker.Patches.Expression;

[TestClass]
public class ExpressionBuilderTests
{
    [TestMethod]
    public void Add_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(1))
            .Add(Push(2))
            .Add(Add)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 3 });
    }

    [TestMethod]
    public void Sub_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(1))
            .Add(Push(2))
            .Add(Sub)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0xFFFFFFFF_FFFFFFFF });
    }

    [TestMethod]
    public void Mul_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(3))
            .Add(Push(2))
            .Add(Mul)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 6 });
    }

    [TestMethod]
    public void And_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0b110011))
            .Add(Push(0b101010))
            .Add(And)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0b100010 });
    }

    [TestMethod]
    public void Or_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0b110011))
            .Add(Push(0b101010))
            .Add(Or)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0b111011 });
    }

    [TestMethod]
    public void Xor_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0b110011))
            .Add(Push(0b101010))
            .Add(Xor)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0b011001 });
    }

    [TestMethod]
    public void Shl_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0b110011))
            .Add(Push(2))
            .Add(Shl)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0b11001100 });
    }

    [TestMethod]
    public void Shr_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0b110011))
            .Add(Push(2))
            .Add(Shr)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0b1100 });
    }

    [TestMethod]
    public void Cpl_Expressions__Are_Reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Push(0x123456789ABCDEF0))
            .Add(Cpl)
            .Build();

        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 0xEDCBA9876543210F });
    }

    [TestMethod]
    public void AddRange_will_reduce_all_expressions()
    {
        var result = new ExpressionBuilder()
            .AddRange(
            [
                Push(1),
                Push(2),
                Add,
                Push(3),
                Push(4),
                Add,
                Add
            ])
            .Build();
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 10 });
    }

    [TestMethod]
    public void Invalid_Binary_Expressions__Are_not_reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Add)
            .Build();
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Add });

        result = new ExpressionBuilder()
            .Add(Push(1))
            .Add(Add)
            .Build();
        Assert.AreEqual(2, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Push, Value: 1 });
        Assert.IsTrue(result.Last() is { Type: ExpressionType.Add });
    }

    [TestMethod]
    public void Invalid_Unary_Expressions__Are_not_reduced()
    {
        var result = new ExpressionBuilder()
            .Add(Cpl)
            .Build();
        Assert.AreEqual(1, result.Count);
        Assert.IsTrue(result.First() is { Type: ExpressionType.Cpl });
    }
}
