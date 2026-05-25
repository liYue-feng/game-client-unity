// NetworkClient.cs — WebSocket 网络客户端
//
// 这是客户端与服务器的唯一通信入口，负责：
//   1. 建立/维护 WebSocket 长连接
//   2. 二进制帧的发送和接收
//   3. 消息路由（按 MsgID 分发到对应的回调）
//   4. 心跳保活（每30秒发送一次心跳请求）
//   5. 断线自动重连
//
// 使用方式：
//   // 获取单例
//   var client = NetworkClient.Instance;
//
//   // 注册消息监听
//   client.On<LoginResp>(MsgID.LoginResp, resp => {
//       Debug.Log($"登录成功: uid={resp.uid}");
//   });
//
//   // 连接服务器
//   client.Connect("ws://localhost:8080/ws");
//
//   // 发送消息
//   client.Send(MsgID.LoginReq, new LoginReq { code = "wx_code" });
//
// 设计模式：
//   - 单例模式：全局唯一，任何脚本都可以方便地访问
//   - 观察者模式：On<T>() 注册监听，收到消息时自动回调
//   - 为什么不用事件总线？因为网络消息是唯一的来源，不需要额外的解耦层

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Game.Protocol;
using UnityEngine;
using WebSocketSharp;

namespace Game.Network
{
    /// <summary>
    /// 网络客户端 —— 与 Go 游戏服务器的 WebSocket 通信
    /// </summary>
    public class NetworkClient : MonoBehaviour
    {
        // ========== 单例 ==========
        private static NetworkClient _instance;
        public static NetworkClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 自动创建 GameObject，确保场景切换时不丢失
                    var go = new GameObject("[NetworkClient]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<NetworkClient>();
                }
                return _instance;
            }
        }

        // ========== 配置 ==========
        [Header("服务器配置")]
        [Tooltip("WebSocket 服务器地址")]
        public string serverUrl = "ws://localhost:8080/ws";

        [Tooltip("心跳间隔（秒）")]
        public float heartbeatInterval = 30f;

        [Tooltip("是否自动重连")]
        public bool autoReconnect = true;

        [Tooltip("重连间隔（秒）")]
        public float reconnectInterval = 5f;

        [Tooltip("最大重连次数")]
        public int maxReconnectAttempts = 5;

        // ========== 状态 ==========
        /// <summary>是否已连接</summary>
        public bool IsConnected => _ws != null && _ws.IsAlive;

        /// <summary>是否已登录</summary>
        public bool IsLoggedIn => _uid > 0;

        /// <summary>当前用户ID</summary>
        public long UID => _uid;

        /// <summary>会话令牌</summary>
        public string Token => _token;

        private long _uid;
        private string _token;
        private WebSocket _ws;
        private float _heartbeatTimer;
        private int _reconnectAttempts;
        private bool _intentionalClose; // 是否主动关闭（主动关闭不自动重连）

        // ========== 消息回调 ==========
        // key = MsgID, value = 回调列表
        // 为什么用 List<Action<string>> 而不是单个 Action？
        //   因为同一个消息可能有多个监听者（如 UI 和数据层都监听登录响应）
        private Dictionary<ushort, List<Action<string>>> _handlers = new Dictionary<ushort, List<Action<string>>>();

        // ========== 事件 ==========
        /// <summary>连接成功事件</summary>
        public event Action OnConnected;

        /// <summary>连接断开事件</summary>
        public event Action OnDisconnected;

        /// <summary>连接错误事件</summary>
        public event Action<string> OnError;

        // ========== 生命周期 ==========

        private void Awake()
        {
            // 确保单例唯一
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (!IsConnected) return;

            // 心跳定时器
            _heartbeatTimer += Time.deltaTime;
            if (_heartbeatTimer >= heartbeatInterval)
            {
                _heartbeatTimer = 0f;
                SendHeartbeat();
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // 应用切到后台时，WebSocket 可能被系统断开
            // 回到前台时尝试重连
            if (!pauseStatus && !IsConnected && autoReconnect)
            {
                StartCoroutine(TryReconnect());
            }
        }

        // ========== 连接管理 ==========

        /// <summary>
        /// 连接服务器
        /// </summary>
        public void Connect(string url = null)
        {
            if (IsConnected) return;

            if (!string.IsNullOrEmpty(url))
                serverUrl = url;

            Debug.Log($"[NetworkClient] 连接服务器: {serverUrl}");
            _intentionalClose = false;

            _ws = new WebSocket(serverUrl);

            // 注册事件
            _ws.OnOpen += OnWsOpen;
            _ws.OnMessage += OnWsMessage;
            _ws.OnClose += OnWsClose;
            _ws.OnError += OnWsError;

            // 异步连接
            _ws.ConnectAsync();
        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Disconnect()
        {
            _intentionalClose = true;
            if (_ws != null && _ws.IsAlive)
            {
                _ws.Close(CloseStatusCode.Normal, "客户端主动断开");
            }
        }

        /// <summary>
        /// 尝试重连
        /// </summary>
        private IEnumerator TryReconnect()
        {
            while (!IsConnected && _reconnectAttempts < maxReconnectAttempts)
            {
                _reconnectAttempts++;
                Debug.Log($"[NetworkClient] 重连中... 第 {_reconnectAttempts}/{maxReconnectAttempts} 次");

                Connect();
                yield return new WaitForSeconds(reconnectInterval);
            }

            if (!IsConnected)
            {
                Debug.LogError("[NetworkClient] 重连失败，已达最大重试次数");
                OnError?.Invoke("连接服务器失败，请检查网络后重试");
            }

            _reconnectAttempts = 0;
        }

        // ========== WebSocket 事件处理 ==========

        private void OnWsOpen(object sender, EventArgs e)
        {
            Debug.Log("[NetworkClient] 连接成功");
            _reconnectAttempts = 0;
            _heartbeatTimer = 0f;
            OnConnected?.Invoke();
        }

        private void OnWsMessage(object sender, MessageEventArgs e)
        {
            // 只处理二进制消息（我们的协议是二进制帧）
            if (!e.IsBinary)
            {
                Debug.LogWarning("[NetworkClient] 收到非二进制消息，已忽略");
                return;
            }

            // 解码消息
            if (!Codec.TryDecode(e.RawData, out ushort msgID, out string body))
            {
                Debug.LogError("[NetworkClient] 消息解码失败");
                return;
            }

            // 分发消息到注册的回调
            // 为什么在主线程执行？因为 Unity 的 API 大多不是线程安全的，
            // WebSocket 的消息回调在工作线程，需要切回主线程
            MainThreadDispatcher.Enqueue(() =>
            {
                DispatchMessage(msgID, body);
            });
        }

        private void OnWsClose(object sender, CloseEventArgs e)
        {
            Debug.Log($"[NetworkClient] 连接断开: code={e.Code} reason={e.Reason}");
            _uid = 0;
            _token = null;
            OnDisconnected?.Invoke();

            // 非主动关闭时自动重连
            if (!_intentionalClose && autoReconnect)
            {
                StartCoroutine(TryReconnect());
            }
        }

        private void OnWsError(object sender, ErrorEventArgs e)
        {
            Debug.LogError($"[NetworkClient] 连接错误: {e.Message}");
            OnError?.Invoke(e.Message);
        }

        // ========== 消息发送 ==========

        /// <summary>
        /// 发送消息（泛型版本，自动序列化）
        /// </summary>
        public void Send<T>(ushort msgID, T payload)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkClient] 未连接，消息发送失败");
                return;
            }

            byte[] frame = Codec.Encode(msgID, payload);
            _ws.Send(frame);
        }

        /// <summary>
        /// 发送消息（原始JSON字符串）
        /// </summary>
        public void Send(ushort msgID, string jsonBody)
        {
            if (!IsConnected)
            {
                Debug.LogWarning("[NetworkClient] 未连接，消息发送失败");
                return;
            }

            byte[] frame = Codec.Encode(msgID, jsonBody);
            _ws.Send(frame);
        }

        /// <summary>
        /// 发送心跳
        /// </summary>
        private void SendHeartbeat()
        {
            var req = new HeartbeatReq
            {
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            Send(MsgID.HeartbeatReq, req);
        }

        // ========== 消息监听 ==========

        /// <summary>
        /// 注册消息监听（泛型版本，自动反序列化）
        ///
        /// 使用方式：
        ///   client.On<LoginResp>(MsgID.LoginResp, resp => {
        ///       Debug.Log($"登录成功: uid={resp.uid}");
        ///   });
        /// </summary>
        public void On<T>(ushort msgID, Action<T> handler)
        {
            if (!_handlers.ContainsKey(msgID))
            {
                _handlers[msgID] = new List<Action<string>>();
            }

            // 包装：收到 JSON 字符串后，反序列化再调用 handler
            _handlers[msgID].Add(body =>
            {
                try
                {
                    T payload = JsonUtility.FromJson<T>(body);
                    handler?.Invoke(payload);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkClient] 消息反序列化失败: msgID={msgID} error={ex.Message}");
                }
            });
        }

        /// <summary>
        /// 注册消息监听（原始JSON字符串版本）
        /// 适用于不需要反序列化的场景（如日志、转发）
        /// </summary>
        public void On(ushort msgID, Action<string> handler)
        {
            if (!_handlers.ContainsKey(msgID))
            {
                _handlers[msgID] = new List<Action<string>>();
            }
            _handlers[msgID].Add(handler);
        }

        /// <summary>
        /// 移除消息监听
        /// </summary>
        public void Off(ushort msgID, Action<string> handler)
        {
            if (_handlers.ContainsKey(msgID))
            {
                _handlers[msgID].Remove(handler);
            }
        }

        // ========== 消息分发 ==========

        private void DispatchMessage(ushort msgID, string body)
        {
            // 特殊处理：错误消息
            if (msgID == MsgID.Error)
            {
                var err = JsonUtility.FromJson<ErrorResp>(body);
                Debug.LogWarning($"[NetworkClient] 服务器错误: code={err.code} msg={err.msg}");
                HandleError(err);
                return;
            }

            // 分发到注册的回调
            if (_handlers.TryGetValue(msgID, out var handlers))
            {
                // 复制一份再遍历，防止回调中修改列表
                var handlersCopy = new List<Action<string>>(handlers);
                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        handler.Invoke(body);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[NetworkClient] 消息处理异常: msgID={msgID} error={ex.Message}");
                    }
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkClient] 未注册的消息ID: {msgID}");
            }
        }

        /// <summary>
        /// 处理服务器错误响应
        /// 根据错误码展示对应的提示
        /// </summary>
        private void HandleError(ErrorResp err)
        {
            switch (err.code)
            {
                case ErrCode.Unauthorized:
                case ErrCode.LoginTokenExpired:
                    Debug.Log("[NetworkClient] 会话过期，需要重新登录");
                    _uid = 0;
                    _token = null;
                    // TODO: 跳转到登录界面
                    break;

                case ErrCode.TooFrequent:
                    Debug.Log("[NetworkClient] 请求过于频繁");
                    // TODO: 显示提示 "操作过于频繁，请稍后再试"
                    break;

                default:
                    Debug.Log($"[NetworkClient] 服务器错误: {err.msg}");
                    break;
            }
        }

        // ========== 登录状态管理 ==========

        /// <summary>
        /// 设置登录信息（登录成功后调用）
        /// </summary>
        public void SetLoginInfo(long uid, string token)
        {
            _uid = uid;
            _token = token;
            Debug.Log($"[NetworkClient] 登录状态已更新: uid={uid}");
        }

        /// <summary>
        /// 清除登录信息（登出时调用）
        /// </summary>
        public void ClearLoginInfo()
        {
            _uid = 0;
            _token = null;
        }
    }
}
