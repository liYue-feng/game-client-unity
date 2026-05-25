// Codec.cs — 二进制帧编解码器
//
// 与服务器端 protocol/codec.go 完全对应。
// 帧格式（小端序）：
//
//   +-------------------+-------------------+-------------------+
//   | Length (4 bytes)  | MsgID  (2 bytes)  | Body   (N bytes)  |
//   +-------------------+-------------------+-------------------+
//
//   Length = 4 + 2 + len(Body) = 6 + len(Body)
//
// 为什么用二进制帧头而不是纯 JSON？
//   - JSON 无法确定消息边界，TCP 是流式协议，可能粘包/拆包
//   - 二进制帧头可以精确读取指定长度的消息，天然解决粘包问题
//   - 服务器和客户端使用相同的编解码逻辑，保证兼容

using System;
using System.Text;

namespace Game.Protocol
{
    /// <summary>
    /// 帧头大小 = 4(Length) + 2(MsgID)
    /// </summary>
    public static class Codec
    {
        public const int HeaderSize = 6;

        /// <summary>
        /// 最大帧长度 64KB
        /// 超过此长度的帧可能是攻击或异常，直接拒绝
        /// </summary>
        public const int MaxFrameSize = 64 * 1024;

        /// <summary>
        /// 将 MsgID + JSON字符串 编码为二进制帧
        ///
        /// 使用方式：
        ///   byte[] frame = Codec.Encode(MsgID.LoginReq, jsonBody);
        ///   webSocket.Send(frame);
        /// </summary>
        public static byte[] Encode(ushort msgID, string jsonBody)
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody ?? "");
            int totalLen = HeaderSize + bodyBytes.Length;

            // 分配缓冲区
            byte[] buf = new byte[totalLen];

            // 写入 Length（小端序 uint32）
            // 为什么用小端序？x86/ARM 都是小端架构，省去字节序转换
            buf[0] = (byte)(totalLen);
            buf[1] = (byte)(totalLen >> 8);
            buf[2] = (byte)(totalLen >> 16);
            buf[3] = (byte)(totalLen >> 24);

            // 写入 MsgID（小端序 uint16）
            buf[4] = (byte)(msgID);
            buf[5] = (byte)(msgID >> 8);

            // 写入 Body
            if (bodyBytes.Length > 0)
            {
                Array.Copy(bodyBytes, 0, buf, HeaderSize, bodyBytes.Length);
            }

            return buf;
        }

        /// <summary>
        /// 将 MsgID + 对象 编码为二进制帧
        /// 自动将对象序列化为 JSON 再编码
        /// </summary>
        public static byte[] Encode<T>(ushort msgID, T payload)
        {
            string json = JsonUtility.ToJson(payload);
            return Encode(msgID, json);
        }

        /// <summary>
        /// 从二进制帧解析出 MsgID 和 Body
        ///
        /// 返回：
        ///   msgID - 消息ID
        ///   body  - JSON字符串（需要外部按具体类型反序列化）
        ///
        /// 使用方式：
        ///   if (Codec.TryDecode(data, out ushort msgID, out string body)) {
        ///       switch (msgID) {
        ///           case MsgID.LoginResp:
        ///               var resp = JsonUtility.FromJson<LoginResp>(body);
        ///               break;
        ///       }
        ///   }
        /// </summary>
        public static bool TryDecode(byte[] data, out ushort msgID, out string body)
        {
            msgID = 0;
            body = null;

            // 检查最小长度
            if (data == null || data.Length < HeaderSize)
                return false;

            // 读取 Length（小端序 uint32）
            uint totalLen = (uint)(data[0] | (data[1] << 8) | (data[2] << 16) | (data[3] << 24));

            // 校验长度一致性
            if (totalLen != data.Length)
                return false;

            // 安全检查
            if (totalLen > MaxFrameSize)
                return false;

            // 读取 MsgID（小端序 uint16）
            msgID = (ushort)(data[4] | (data[5] << 8));

            // 提取 Body
            if (totalLen > HeaderSize)
            {
                body = Encoding.UTF8.GetString(data, HeaderSize, (int)(totalLen - HeaderSize));
            }
            else
            {
                body = "";
            }

            return true;
        }
    }
}
