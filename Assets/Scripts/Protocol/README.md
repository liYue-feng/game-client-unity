# Protocol 兼容性说明

本目录的代码与 Go 游戏服务器 (game-server-go) 的协议层一一对应：

| 客户端文件 | 对应服务器文件 | 说明 |
|-----------|---------------|------|
| Protocol.cs | protocol/message.go + common.go | 消息ID + 错误码 |
| Messages.cs | protocol/message.go | 请求/响应结构体 |
| Codec.cs | protocol/codec.go | 二进制帧编解码 |

**修改协议时必须同步更新两端代码！**

## 帧格式
```
+-------------------+-------------------+-------------------+
| Length (4 bytes)  | MsgID  (2 bytes)  | Body   (N bytes)  |
+-------------------+-------------------+-------------------+
  小端序 uint32       小端序 uint16        JSON 编码的消息体
```
