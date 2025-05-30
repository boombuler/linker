namespace boombuler.Linker;

using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text;



public ref struct Writer
{
    const int MinBufferSize = 128;
    private static readonly Encoding fEncoding = Encoding.UTF8;
    private Span<byte> fBuffer;
    private readonly Stream fStream;
    private int fOffset;

    public readonly long Position => fStream.Position + fOffset;

    public Writer(Span<byte> buffer, Stream stream)
    {
        if (buffer.Length < MinBufferSize)
            throw new ArgumentException($"Buffer must be at least {MinBufferSize} bytes long", nameof(buffer));

        ArgumentNullException.ThrowIfNull(stream);

        fBuffer = buffer;
        fStream = stream;
        fOffset = 0;
    }

    public void WriteByte(byte value)
    {
        EnsureFreeByte();
        fBuffer[fOffset++] = value;
    }

    public void WriteBool(bool value)
    {
        EnsureFreeByte();
        fBuffer[fOffset++] = (byte)(value ? 1 : 0);
    }

    public void WriteUInt16(ushort value)
    {
        EnsureBuffer(sizeof(ushort));
        BinaryPrimitives.WriteUInt16LittleEndian(fBuffer.Slice(fOffset), value);
        fOffset += sizeof(ushort);
    }

    public void WriteUInt32(uint value)
    {
        EnsureBuffer(sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(fBuffer.Slice(fOffset), value);
        fOffset += sizeof(uint);
    }

    public void WriteUInt64(ulong value)
    {
        EnsureBuffer(sizeof(ulong));
        BinaryPrimitives.WriteUInt64LittleEndian(fBuffer.Slice(fOffset), value);
        fOffset += sizeof(ulong);
    }

    public void WriteVarInt(int value)
    {
        while (value >= 0x80)
        {
            WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        WriteByte((byte)value);
    }

    public void WriteString(ReadOnlySpan<char> value)
    {
        // UTF16 -> UTF8 takes at most 3 times the space
        // If that value can be encoded within a single byte VarInt
        // and if we have enough space in the buffer, we can write it directly

        // Stryker disable once Equality: Modification from `<=` to `<` will also work but might be slower.
        if (value.Length <= 127 / 3)
        {
            EnsureBuffer(128);
            var target = fBuffer.Slice(fOffset + 1);
            var actualBytes = fEncoding.GetBytes(value, target);
            fBuffer[fOffset] = (byte)actualBytes;
            fOffset += actualBytes + 1;
        }
        else 
        {
            WriteVarInt(fEncoding.GetByteCount(value));

            Encoder encoder = fEncoding.GetEncoder();
            bool completed;

            EnsureFreeByte();
            do
            {
                // Stryker disable once Boolean: the flush parameter should have no effect as all remaining input is passed to the Convert method.
                encoder.Convert(value, fBuffer.Slice(fOffset), flush: true, out int charsConsumed, out int bytesWritten, out completed);
                fOffset += bytesWritten;
                if (bytesWritten != 0)
                    Flush();

                value = value.Slice(charsConsumed);
            } while (!completed);
        }
    }

    public void Write(params ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length <= fBuffer.Length)
        {
            EnsureBuffer(buffer.Length);
            buffer.CopyTo(fBuffer.Slice(fOffset));
            fOffset += buffer.Length;
        }
        else
        {
            Flush();
            fStream.Write(buffer);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureBuffer(int space)
    {
        // Stryker disable once Equality: Modification from `>` to `>=` will also work but an additional flush might be slower.
        if (fOffset + space > fBuffer.Length)
            Flush();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureFreeByte()
    {
        if (fOffset >= fBuffer.Length)
            Flush();
    }

    public void Flush()
    {
        fStream.Write(fBuffer.Slice(0, fOffset));
        fOffset = 0;
    }
}