﻿namespace boombuler.Linker.Module;

using System;
using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Numerics;
using System.Runtime.InteropServices;
using boombuler.Linker;
using boombuler.Linker.Patches;

public class ModuleSerializer<TAddr>
    where TAddr : struct, IUnsignedNumber<TAddr>, INumberBase<TAddr>, IShiftOperators<TAddr, int, TAddr>, IBitwiseOperators<TAddr, TAddr, TAddr>
{
    private static readonly byte[] MagicNumber = [ 
        0x42, 0x4C, 0x4B, // BLK
        0x01,             // Version 1
        0x00              // Address Size
    ];
    private static readonly Index VersionIdx = new Index(3);
    private static readonly Index AddressLengthIdx = new Index(4);

    private static byte AddressLength => MagicNumber[AddressLengthIdx];

    static ModuleSerializer()
    {
        MagicNumber[^1] = (byte)Marshal.SizeOf<TAddr>();
    }

    public void Serialize(Module<TAddr> module, Stream target)
    {
        ArgumentNullException.ThrowIfNull(module);
        ArgumentNullException.ThrowIfNull(target);
        target.Write(MagicNumber);
        
        Span<byte> buffer = stackalloc byte[1024];
        var writer = new Writer(buffer, target);
        writer.WriteString(module.Name);
        writer.WriteVarInt(module.Symbols.Length);
        foreach (var symbol in module.Symbols)
            WriteSymbol(ref writer, symbol);

        writer.WriteVarInt(module.Sections.Length);
        foreach (var section in module.Sections)
            WriteSection(ref writer, section);

        writer.Flush();
    }

    private void WriteSection(ref Writer writer, Section<TAddr> section) 
    {
        writer.WriteString(section.Region.Value);
        writer.WriteVarInt(section.SymbolAddresses.Count);
        foreach (var (id, addr) in section.SymbolAddresses)
        {
            writer.WriteVarInt(id.Value);
            WriteAddr(ref writer, addr);
        }
        writer.WriteBool(section.Origin.HasValue);
        if (section.Origin.HasValue)
            WriteAddr(ref writer, section.Origin.Value);
        WriteAddr(ref writer, section.Alignment);
        WriteAddr(ref writer, section.Size);
        
        writer.WriteVarInt(section.Data.Length);
        writer.Write(section.Data.Span);

        writer.WriteVarInt(section.Patches.Length);
        foreach (var patch in section.Patches)
        {
            WriteAddr(ref writer, patch.Location);
            writer.WriteByte(patch.Size);
            writer.WriteVarInt(patch.Expressions.Count);
            foreach (var expr in patch.Expressions)
                WritePatchExpr(ref writer, expr);
        }
    }

    private void WritePatchExpr(ref Writer writer, Expression expr)
    {
        var op = (byte)expr.Type;
        if (expr.Value == 0)
            writer.WriteByte(op);
        else if (expr.Value > ushort.MaxValue)
        {
            if (expr.Value > uint.MaxValue)
            {
                writer.WriteByte((byte)(op | 0x80));
                writer.WriteUInt64(expr.Value);
            }
            else
            {
                writer.WriteByte((byte)(op | 0x40));
                writer.WriteUInt32((uint)expr.Value);
            }
        }
        else
        {
            if (expr.Value > byte.MaxValue)
            {
                writer.WriteByte((byte)(op | 0x20));
                writer.WriteUInt16((ushort)expr.Value);
            }
            else
            {
                writer.WriteByte((byte)(op | 0x10));
                writer.WriteByte((byte)expr.Value);
            }
        }
    }

    private void WriteAddr(ref Writer writer, TAddr value)
    {
        for (int i = 0; i < AddressLength; i++)
        {
            writer.WriteByte(byte.CreateTruncating(value));
            value >>= 8;
        }
    }

    private static void WriteSymbol(ref Writer writer, Symbol symbol)
    {
        writer.WriteByte((byte)symbol.Type);
        writer.WriteString(symbol.Name.Global ?? string.Empty);
        writer.WriteString(symbol.Name.Local ?? string.Empty);
    }


    public Module<TAddr> Deserialize(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        using var binaryReader = new BinaryReader(source, System.Text.Encoding.UTF8, leaveOpen: true);
        ValidateFileHeader(binaryReader);

        var moduleName = binaryReader.ReadString();
        var symbols = ReadSymbols(binaryReader, binaryReader.Read7BitEncodedInt()).ToImmutableArray();
        var sections = ReadSections(binaryReader, binaryReader.Read7BitEncodedInt()).ToImmutableArray();
        return new Module<TAddr>()
        {
            Name = moduleName,
            Symbols = symbols,
            Sections = sections,
        };
    }

    private IEnumerable<Section<TAddr>> ReadSections(BinaryReader binaryReader, int count)
    { 
        for (int i = 0; i < count; i++)
        {
            var region = binaryReader.ReadString() ?? string.Empty;
            int symbolCount = binaryReader.Read7BitEncodedInt();
            var symbolAddresses = new Dictionary<SymbolId, TAddr>(symbolCount);
            for (int j = 0; j < symbolCount; j++)
            {
                var id = new SymbolId(binaryReader.Read7BitEncodedInt());
                symbolAddresses[id] = ReadAddr(binaryReader);
            }

            bool hasOrigin = binaryReader.ReadBoolean();
            TAddr? origin = hasOrigin ? ReadAddr(binaryReader) : null;
            var alignment = ReadAddr(binaryReader);
            var size = ReadAddr(binaryReader);
            int dataLength = binaryReader.Read7BitEncodedInt();
            byte[] data = binaryReader.ReadBytes(dataLength);
            var patches = ReadPatches(binaryReader, binaryReader.Read7BitEncodedInt());

            yield return new Section<TAddr>() {
                Region = new RegionName(region),
                SymbolAddresses = symbolAddresses.ToFrozenDictionary(),
                Origin = origin,
                Alignment = alignment,
                Size = size,
                Data = data,
                Patches = patches
            };
        }
    }

    private ImmutableArray<Patch<TAddr>> ReadPatches(BinaryReader binaryReader, int patchCount)
    {
        var patches = ImmutableArray.CreateBuilder<Patch<TAddr>>(patchCount);
        for (int i = 0; i < patchCount; i++)
        {
            var location = ReadAddr(binaryReader);
            byte sizeOfPatch = binaryReader.ReadByte();
            int exprCount = binaryReader.Read7BitEncodedInt();
            var expressions = new Expression[exprCount];
            for (int j = 0; j < exprCount; j++)
                expressions[j] = ReadExpression(binaryReader);
            patches.Add(new Patch<TAddr>()
            {
                Location = location,
                Size = sizeOfPatch,
                Expressions = expressions
            });
        }
            
        return patches.ToImmutable();
    }

    private Expression ReadExpression(BinaryReader reader)
    {
        var op = reader.ReadByte();
        var type = (ExpressionType)(op & 0x0F);
        ulong arg = ((op & 0xF0) >> 4) switch
        {
            1 => reader.ReadByte(),
            2 => reader.ReadUInt16(),
            3 => reader.ReadUInt32(),
            4 => reader.ReadUInt64(),
            _ => 0,
        };
        return new Expression { Type = type, Value = arg };
    }

    private IEnumerable<Symbol> ReadSymbols(BinaryReader reader, int count)
    {
        while(count-- > 0)
        {
            var type = (SymbolType)reader.ReadByte();
            if (!Enum.IsDefined(type))
                throw new ArgumentException($"Invalid Symbol-Type {type}");
            string globalName = reader.ReadString() ?? string.Empty;
            string? localName = reader.ReadString() is string s and not "" ? s : null;
            yield return new Symbol(new SymbolName(globalName, localName), type);
        }
    }

    private TAddr ReadAddr(BinaryReader binaryReader)
    {
        TAddr addr = TAddr.Zero;
        for (int i = 0; i < AddressLength; i++)
        {
            byte b = binaryReader.ReadByte();
            // Stryker disable once Assignment: |= results in the same value as ^= as addr is initialized with zero.
            addr |= TAddr.CreateTruncating(b) << (i * 8);
        }
        return addr;
    }

    private static void ValidateFileHeader(BinaryReader binaryReader)
    {
        Span<byte> buff = stackalloc byte[MagicNumber.Length];
        int read = binaryReader.Read(buff);
        // Stryker disable once Equality: Leaving out the length check will 
        // work in almost all cases, but since the stack alloc might contain
        // a valid magic number, we need to check the length as well.
        if (read < MagicNumber.Length || !buff[0..3].SequenceEqual(MagicNumber[0..3]))
            throw new ArgumentException("Invalid Module File");

        if (buff[VersionIdx] != 1)
            throw new ArgumentException($"File Version {buff[VersionIdx]} not supported");

        if (buff[AddressLengthIdx] != AddressLength)
            throw new ArgumentException($"Address Length missmatch. (Expected {AddressLength} got {buff[AddressLengthIdx]}");
    }
}
