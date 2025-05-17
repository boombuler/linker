using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace boombuler.Linker.Module
{
    ref struct Writer
    {
        private static readonly Encoding fEncoding = Encoding.UTF8;
        private Span<byte> fBuffer;
        private Stream fStream;
        private int fOffset;

        public Writer(Span<byte> buffer, Stream stream)
        {
            fBuffer = buffer;
            fStream = stream;
            fOffset = 0;
        }

        public void WriteByte(byte value)
        {
            fBuffer[fOffset++] = value;
            FlushIfNeeded();
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
            if (value.Length <= (127 / 3) && (fBuffer.Length - fOffset) >= 128)
            {
                var target = fBuffer.Slice(fOffset + 1);
                var actualBytes = fEncoding.GetBytes(value, target);
                fBuffer[fOffset] = (byte)actualBytes;
                fOffset += actualBytes + 1;
                FlushIfNeeded();
            }
            else 
            {
                WriteVarInt(fEncoding.GetByteCount(value));

                Encoder encoder = fEncoding.GetEncoder();
                bool completed;

                do
                {
                    encoder.Convert(value, fBuffer.Slice(fOffset), flush: true, out int charsConsumed, out int bytesWritten, out completed);
                    fOffset += bytesWritten;
                    if (bytesWritten != 0)
                        Flush();

                    value = value.Slice(charsConsumed);
                } while (!completed);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FlushIfNeeded()
        {
            if (fOffset >= fBuffer.Length)
                Flush();
        }

        public void Flush()
        {
            if (fOffset == 0)
                return;
            fStream.Write(fBuffer.Slice(0, fOffset));
            fOffset = 0;
        }
    }
}
