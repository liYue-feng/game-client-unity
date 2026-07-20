using System;
using Google.Protobuf;

namespace Game.Protocol
{
    public static class Codec
    {
        public const int HeaderSize = 6;
        public const int MaxFrameSize = 64 * 1024;

        public static byte[] Encode(ushort msgID, IMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var body = message.ToByteArray();
            var totalLength = HeaderSize + body.Length;
            if (totalLength > MaxFrameSize)
            {
                throw new ArgumentOutOfRangeException(nameof(message), "Frame exceeds the 64 KiB protocol limit.");
            }

            var frame = new byte[totalLength];
            frame[0] = (byte)totalLength;
            frame[1] = (byte)(totalLength >> 8);
            frame[2] = (byte)(totalLength >> 16);
            frame[3] = (byte)(totalLength >> 24);
            frame[4] = (byte)msgID;
            frame[5] = (byte)(msgID >> 8);
            if (body.Length > 0)
            {
                Array.Copy(body, 0, frame, HeaderSize, body.Length);
            }

            return frame;
        }

        public static bool TryDecode(byte[] data, out ushort msgID, out byte[] body)
        {
            msgID = 0;
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
