using UnityEngine;
using Game.Managers;

/// <summary>
/// 主菜单场景启动器 — 初始化场景管理器 + 显示登录/菜单
/// </summary>
public class MenuSceneSetup : MonoBehaviour
{
    void Awake()
    {
        // 确保场景管理器存在
        if (SceneTransitionManager.Instance == null)
        {
            var go = new GameObject("SceneTransitionManager");
            go.AddComponent<SceneTransitionManager>();
        }

        // 确保网络基础设施存在
        if (HeartbeatManager.Instance == null)
        {
            var go = new GameObject("HeartbeatManager");
            go.AddComponent<HeartbeatManager>();
        }

        if (ReconnectionManager.Instance == null)
        {
            var go = new GameObject("ReconnectionManager");
            go.AddComponent<ReconnectionManager>();
        }

        // 初始化 LoginManager（如果不存在）
        if (LoginManager.Instance == null)
        {
            var go = new GameObject("LoginManager");
            go.AddComponent<LoginManager>();
        }
    }

    void Start()
    {
        // 显示登录界面
        // LoginManager 登录成功后，由 MainMenuUI 接管显示
        var loginGo = new GameObject("LoginUI");
        loginGo.AddComponent<LoginUI>().loginManager = LoginManager.Instance;

        // 登录成功后的事件（在LoginManager中触发）
        // 这里暂且同时准备好 MainMenuUI（在登录成功后切换）
    }
}