// LoginManager.cs — 登录管理器
//
// 负责微信小游戏登录流程的客户端部分：
//   1. 调用 wx.login() 获取临时 code（微信SDK）
//   2. 将 code 发送给服务器
//   3. 接收登录响应，保存 uid 和 token
//   4. 管理登录状态
//
// 微信小游戏登录流程：
//   客户端 wx.login() → code → 服务器 → 微信API → openid → 注册/查找玩家 → 返回 uid+token
//
// 注意：微信 SDK 的调用需要在 Unity 中接入微信小游戏 SDK。
// 开发阶段可以先使用测试 code 进行调试。

using System;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Managers
{
    public class LoginManager : MonoBehaviour
    {
        private static LoginManager _instance;
        public static LoginManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[LoginManager]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<LoginManager>();
                }
                return _instance;
            }
        }

        /// <summary>当前用户ID</summary>
        public long UID => NetworkClient.Instance.UID;

        /// <summary>当前昵称</summary>
        public string Nickname { get; private set; }

        /// <summary>是否已登录</summary>
        public bool IsLoggedIn => NetworkClient.Instance.IsLoggedIn;

        /// <summary>登录成功事件</summary>
        public event Action<LoginResp> OnLoginSuccess;

        /// <summary>登录失败事件</summary>
        public event Action<string> OnLoginFailed;

        /// <summary>登出事件</summary>
        public event Action OnLogout;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            // 注册消息监听
            var client = NetworkClient.Instance;
            client.On<LoginResp>(MsgID.LoginResp, HandleLoginResp);
        }

        /// <summary>
        /// 发起微信登录
        /// 在微信小游戏环境中，会调用 wx.login() 获取 code
        /// 在开发环境中，使用测试 code
        /// </summary>
        public void WechatLogin()
        {
            // TODO: 接入微信小游戏 SDK
            // 微信环境下的代码：
            // WX.Login(code => {
            //     SendLoginReq(code);
            // });

            // 开发阶段：使用测试 code
            SendLoginReq("test_dev_code");
        }

        /// <summary>
        /// 使用 code 发送登录请求
        /// </summary>
        public void SendLoginReq(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                OnLoginFailed?.Invoke("登录code为空");
                return;
            }

            Debug.Log($"[LoginManager] 发送登录请求: code={code}");
            NetworkClient.Instance.Send(MsgID.LoginReq, new LoginReq { code = code });
        }

        /// <summary>
        /// 处理登录响应
        /// </summary>
        private void HandleLoginResp(LoginResp resp)
        {
            Debug.Log($"[LoginManager] 登录成功: uid={resp.uid} nickname={resp.nickname}");

            // 保存登录信息到 NetworkClient
            NetworkClient.Instance.SetLoginInfo(resp.uid, resp.token);

            // 保存昵称
            Nickname = resp.nickname;

            // 触发事件
            OnLoginSuccess?.Invoke(resp);
        }

        /// <summary>
        /// 登出
        /// </summary>
        public void Logout()
        {
            NetworkClient.Instance.ClearLoginInfo();
            Nickname = null;
            OnLogout?.Invoke();
        }
    }
}
