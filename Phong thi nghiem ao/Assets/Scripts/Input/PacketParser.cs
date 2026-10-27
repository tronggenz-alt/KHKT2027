using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Khkt.Input
{
    public struct RawPacket
    {
        public byte Seq;
        public uint TimeMs;
        public short Ax, Ay, Az, Gx, Gy, Gz;
        public ushort J1x, J1y, J2x, J2y;
        public byte Buttons;
    }

    /// <summary>
    /// Giải mã gói 29 byte từ firmware (docs/02-giao-thuc-tay-cam.md). Bản Python tương đương: khkt/protocol.py.
    /// Không an toàn đa luồng: chỉ gọi Feed từ một luồng.
    /// </summary>
    public sealed class PacketParser
    {
        public const int Length = 29;
        public const float AccLsbPerG = 16384f;
        public const float GyroLsbPerDps = 65.5f;

        public int CrcErrors { get; private set; }
        public int LostPackets { get; private set; }
        /// <summary>Các dòng '#...' firmware gửi (thông báo khởi động, hiệu chuẩn).</summary>
        public readonly ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();

        readonly List<byte> buf = new List<byte>(256);
        readonly StringBuilder text = new StringBuilder();
        int lastSeq = -1;

        public void Feed(byte[] data, int count, List<RawPacket> output)
        {
            for (int i = 0; i < count; i++) buf.Add(data[i]);
            int pos = 0;
            while (true)
            {
                int s = FindSync(pos);
                if (s < 0)
                {
                    int keep = buf.Count > pos && buf[buf.Count - 1] == 0xA5 ? buf.Count - 1 : buf.Count;
                    SkipText(pos, keep);
                    pos = keep;
                    break;
                }
                SkipText(pos, s);
                pos = s;
                if (buf.Count - pos < Length) break;
                if (Crc8(pos + 2, 26) != buf[pos + 28])
                {
                    CrcErrors++;
                    SkipText(pos, pos + 1);
                    pos += 1;
                    continue;
                }
                var p = Decode(pos);
                if (lastSeq >= 0) LostPackets += (p.Seq - lastSeq - 1 + 256) % 256;
                lastSeq = p.Seq;
                output.Add(p);
                pos += Length;
            }
            buf.RemoveRange(0, pos);
        }

        int FindSync(int from)
        {
            for (int i = from; i + 1 < buf.Count; i++)
                if (buf[i] == 0xA5 && buf[i + 1] == 0x5A) return i;
            return -1;
        }

        byte Crc8(int start, int len)
        {
            byte crc = 0;
            for (int i = start; i < start + len; i++)
            {
                crc ^= buf[i];
                for (int b = 0; b < 8; b++)
                    crc = (crc & 0x80) != 0 ? (byte)((crc << 1) ^ 0x07) : (byte)(crc << 1);
            }
            return crc;
        }

        ushort U16(int i) => (ushort)(buf[i] | (buf[i + 1] << 8));
        short I16(int i) => (short)U16(i);

        RawPacket Decode(int p)
        {
            return new RawPacket
            {
                Seq = buf[p + 2],
                TimeMs = (uint)(buf[p + 3] | (buf[p + 4] << 8) | (buf[p + 5] << 16) | (buf[p + 6] << 24)),
                Ax = I16(p + 7), Ay = I16(p + 9), Az = I16(p + 11),
                Gx = I16(p + 13), Gy = I16(p + 15), Gz = I16(p + 17),
                J1x = U16(p + 19), J1y = U16(p + 21), J2x = U16(p + 23), J2y = U16(p + 25),
                Buttons = buf[p + 27],
            };
        }

        void SkipText(int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                char c = (char)buf[i];
                if (c == '\n')
                {
                    string line = text.ToString();
                    int k = line.IndexOf('#');
                    if (k >= 0) Messages.Enqueue(line.Substring(k + 1).Trim());
                    text.Clear();
                }
                else if (text.Length < 256) text.Append(c);
                else text.Clear();
            }
        }
    }
}
