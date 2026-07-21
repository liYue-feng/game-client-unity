# Protocol

`proto/game.proto` is the byte-identical shared schema for all 32 WebSocket
route IDs. Generated protobuf messages live in `Generated/Game.cs`; regenerate
them with `tools/protobuf/Generate-Protocol.ps1` and do not edit them by hand.

`Codec` owns only the six-byte little-endian envelope:

```text
uint32 little-endian total length
uint16 little-endian message ID
protobuf message bytes
```

The total length includes the header and frames larger than 64 KiB are rejected.
`ProtocolMessageRegistry` is the explicit MsgID-to-generated-parser table used by
the typed `NetworkClient` boundary. The envelope never carries JSON.

The vendored runtime is Google.Protobuf 3.35.1 `net45`, with its required
`System.Runtime.CompilerServices.Unsafe` dependency for Unity's loader.
