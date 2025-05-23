namespace boombuler.Linker.Tests;

using System;
using boombuler.Linker.Patches;

[TestClass]
public class PatchTests
{
    [TestMethod]
    [DataRow(0x01u, 0x00u, 0x01u)]
    [DataRow(0xFFu, 0x01u, 0x00u)]
    [DataRow(0xF0u, 0x01u, 0xF1u)]
    public void Add__Adds_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);

        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Add], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x0000u)]
    [DataRow(0x1234u)]
    [DataRow(0xFFFFu)]
    public void CurrentAddress__Writes_the_Target_Address_To_The_Buffer(uint address)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run((ushort)address, 2, [Expression.CurrentAdress], ms);
        CollectionAssert.AreEqual(new byte[] { 
            (byte)(address & 0xFF),
            (byte)((address & 0xFF00) >> 8)
        }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x01u, 0x01u, 0x00u)]
    [DataRow(0x03u, 0x01u, 0x02u)]
    [DataRow(0x00u, 0x01u, 0xFFu)]
    [DataRow(0xF1u, 0x01u, 0xF0u)]
    public void Sub__Substracts_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);

        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Sub], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x01u, 0x01u, 0x01u)]
    [DataRow(0x03u, 0x10u, 0x30u)]
    [DataRow(0x9Fu, 0x02u, 0x3Eu)]
    public void Mul__Multiplies_Two_Values_with_Overflow(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);

        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Mul], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x0123456789ABCDEFu)]
    [DataRow(0x0u)]
    [DataRow(0xFFFFFFFFFFFFFFFFu)]
    public void Cpl__Flips_all_bits(ulong a)
    {
        ulong expected = ~a;
        var runtime = new PatchRuntime<ulong>(s => (ushort)s.Value);

        using var ms = new MemoryStream();
        runtime.Run(0x0000, 8, [Expression.Push(a), Expression.Cpl], ms);
        CollectionAssert.AreEqual(new byte[] { 
            (byte)((expected >> 0) & 0xFF),
            (byte)((expected >> 8) & 0xFF),
            (byte)((expected >> 16) & 0xFF),
            (byte)((expected >> 24) & 0xFF),
            (byte)((expected >> 32) & 0xFF),
            (byte)((expected >> 40) & 0xFF),
            (byte)((expected >> 48) & 0xFF),
            (byte)((expected >> 56) & 0xFF),
        }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x01u, 0x00u, 0x00u)]
    [DataRow(0xFFu, 0x01u, 0x01u)]
    [DataRow(0xFFu, 0xFFu, 0xFFu)]
    [DataRow(0x00u, 0x00u, 0x00u)]
    [DataRow(0b1010u, 0b1100u, 0b1000u)]
    public void And__Bitwise_Ands_Two_Values(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.And], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x01u, 0x00u, 0x01u)]
    [DataRow(0xFFu, 0x01u, 0xFFu)]
    [DataRow(0xFFu, 0xFFu, 0xFFu)]
    [DataRow(0x00u, 0x00u, 0x00u)]
    [DataRow(0b1010u, 0b1100u, 0b1110u)]
    public void Or__Bitwise_Ors_Two_Values(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Or], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x01u, 0x00u, 0x01u)]
    [DataRow(0xFFu, 0x01u, 0xFEu)]
    [DataRow(0xFFu, 0xFFu, 0x00u)]
    [DataRow(0x00u, 0x00u, 0x00u)]
    [DataRow(0b1010u, 0b1100u, 0b0110u)]
    public void Xor__Bitwise_Xors_Two_Values(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Xor], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0x0000, 0xFFFFu)]
    [DataRow(0x0011, 0xFFEEu)]
    public void SymbolAddress__Resolves_the_Symbol_Adress(int symbol, uint expected)
    {
        var runtime = new PatchRuntime<ulong>(s => (ulong)~s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 2, [Expression.SymbolAdress(new SymbolId(symbol))], ms);
        CollectionAssert.AreEqual(new byte[] {
            (byte)(expected & 0xFF),
            (byte)((expected & 0xFF00) >> 8)
        }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0b0000_0001u, 3u, 0b0000_1000u)]
    [DataRow(0b1000_1001u, 1u, 0b0001_0010u)]
    public void Shl__Shifts_the_Value_Left(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Shl], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    [DataRow(0b0000_1000u, 3u, 0b0000_0001u)]
    [DataRow(0b0001_0010u, 1u, 0b0000_1001u)]
    public void Shr__Shifts_the_Value_Right(uint a, uint b, uint expected)
    {
        var runtime = new PatchRuntime<ushort>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 1, [Expression.Push(a), Expression.Push(b), Expression.Shr], ms);
        CollectionAssert.AreEqual(new byte[] { (byte)expected }, ms.ToArray());
    }

    [TestMethod]
    public void Runtime_without_Symbol_Resolver__Crashes()
    {
        Assert.ThrowsException<ArgumentNullException>(() => new PatchRuntime<ushort>(null!));
    }

    [TestMethod]
    public void Zero_Length_Patches__Do_not_modify_the_Buffer()
    {
        var runtime = new PatchRuntime<ulong>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        runtime.Run(0x0000, 0, [Expression.Push(0xFF)] , ms);
        CollectionAssert.AreEqual(new byte[] { }, ms.ToArray());
    }

    [TestMethod]
    public void Incomplete_Patch_Expressions__Raises_Exceptions()
    {
        var runtime = new PatchRuntime<ulong>(s => (ushort)s.Value);
        using var ms = new MemoryStream();
        Assert.ThrowsException<InvalidOperationException>(() => runtime.Run(0x0000, 1, [Expression.Push(5), Expression.Push(1)], ms));
    }
}
