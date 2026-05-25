// GameBootstrap.cs — 游戏启动入口
//
// 这是整个客户端的初始化入口，负责：
//   1. 初始化所有 Manager（按正确顺序）
//   2. 建立服务器连接
//   3. 发起登录
//
// 使用方式：
//   在首个场景中创建空 GameObject，挂载此脚本。
//   此脚本会自动创建所有需要的 Manager 实例。
//
// 初始化顺序（重要！）：
//   MainThreadDispatcher → NetworkClient → LoginManager → ArchiveManager → RankManager
//   必须按此顺序，因为后面的 Manager 依赖前面的。

using Game.Managers;
using Game.Network;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// 游戏启动引导脚本
    /// 挂载在首个场景的 GameObject 上即可
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("服务器配置")]
        [Tooltip("WebSocket 服务器地址")]
        public string serverUrl = "ws://localhost:8080/ws";

        private void Start()
        {
            Debug.Log("========== 游戏启动 ==========");

            // 1. 初始化主线程调度器（最先创建，其他组件依赖它）
            var dispatcher = MainThreadDispatcher.Instance;
            Debug.Log("[Bootstrap] MainThreadDispatcher 初始化完成");

            // 2. 初始化网络客户端
            var netClient = NetworkClient.Instance;
            netClient.serverUrl = serverUrl;
            Debug.Log("[Bootstrap] NetworkClient 初始化完成");

            // 3. 初始化业务管理器
            var loginMgr = LoginManager.Instance;
            var archiveMgr = ArchiveManager.Instance;
            var rankMgr = RankManager.Instance;
            Debug.Log("[Bootstrap] Managers 初始化完成");

            // 4. 注册连接成功后的自动登录
            netClient.OnConnected += () =>
            {
                Debug.Log("[Bootstrap] 连接成功，开始登录...");
                loginMgr.WechatLogin();
            };

            // 5. 注册登录成功后的自动加载存档
            loginMgr.OnLoginSuccess += (resp) =>
            {
                Debug.Log($"[Bootstrap] 登录成功，加载存档...");
                archiveMgr.LoadArchive();
            };

            // 6. 连接服务器
            Debug.Log($"[Bootstrap] 连接服务器: {serverUrl}");
            netClient.Connect();
        }
    }
}
