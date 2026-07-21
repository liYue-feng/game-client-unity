using System;
using Google.Protobuf;

namespace Game.Protocol
{
    public static class Codec
    {
        public const int HeaderSize = 10;
        public const int MaxFrameSize = 64 * 1024;

        public static byte[] Encode(ushort msgID, uint seq, byte[] body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var totalLength = HeaderSize + body.Length;
            if (totalLength > MaxFrameSize)
            {
                throw new ArgumentOutOfRangeException(nameof(body), "Frame exceeds the 64 KiB protocol limit.");
            }

            var frame = new byte[totalLength];
            frame[0] = (byte)totalLength;
            frame[1] = (byte)(totalLength >> 8);
            frame[2] = (byte)(totalLength >> 16);
            frame[3] = (byte)(totalLength >> 24);
            frame[4] = (byte)msgID;
            frame[5] = (byte)(msgID >> 8);
            frame[6] = (byte)seq;
            frame[7] = (byte)(seq >> 8);
            frame[8] = (byte)(seq >> 16);
            frame[9] = (byte)(seq >> 24);
            if (body.Length > 0)
            {
                Array.Copy(body, 0, frame, HeaderSize, body.Length);
            }

            return frame;
        }

        public static byte[] Encode(ushort msgID, uint seq, IMessage message)
            => Encode(msgID, seq, message.ToByteArray());

        public static bool TryDecode(byte[] data, out ushort msgID, out uint seq, out byte[] body)
        {
            msgID = 0;
            seq = 0;
            body = null;
            if (data == null || data.Length < HeaderSize)
            {
                return false;
            }

            var totalLength = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));
            if (totalLength != data.Length || totalLength > MaxFrameSize)
            {
                return false;
            }

            msgID = (ushort)(data[4] | (data[5] << 8));
            seq = (uint)(data[6] | (data[7] << 8) | (data[8] << 16) | (data[9] << 24));
            var bodyLength = data.Length - HeaderSize;
            body = new byte[bodyLength];
            if (bodyLength > 0)
            {
                Array.Copy(data, HeaderSize, body, 0, bodyLength);
            }

            return true;
        }

    }
}
