using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    /// <summary>
    /// 验证真实战斗场景可以在不启动联网流程的情况下完成最小初始化。
    /// </summary>
    public sealed class BattleSceneOfflineSmokeTests
    {
        /// <summary>
        /// 加载 Build Settings 中的战斗场景并检查核心对象。
        /// </summary>
        /// <returns>等待场景和延迟一帧初始化完成的枚举器。</returns>
        [UnityTest]
        public IEnumerator BattleSceneStartsOfflineAndCreatesCoreObjects()
        {
            var failures = new List<string>();
            void CaptureFailure(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                {
                    failures.Add($"{type}: {condition}\n{stackTrace}");
                }
            }

            Application.logMessageReceived += CaptureFailure;
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                yield return SceneManager.LoadSceneAsync("BattleScene", LoadSceneMode.Single);
                yield return null;
                yield return null;

                Assert.That(failures, Is.Empty, string.Join("\n\n", failures));
                Assert.That(GameObject.Find("Ground"), Is.Not.Null, "战斗场景必须创建地面");
                Assert.That(GameObject.Find("Player"), Is.Not.Null, "战斗场景必须创建玩家");
                Assert.That(GameObject.Find("WaveSpawner"), Is.Not.Null, "战斗场景必须创建刷怪器");
                Assert.That(GameObject.Find("[BattleHUD]"), Is.Not.Null, "战斗场景必须创建战斗 HUD");
                Assert.That(GameObject.Find("[NetworkClient]"), Is.Null, "离线场景不得创建网络客户端");
                Assert.That(GameObject.Find("[LoginManager]"), Is.Null, "离线场景不得启动登录流程");
                Assert.That(GameObject.Find("[GameBootstrap]"), Is.Null, "离线场景不得启动在线 Bootstrap");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
                Application.logMessageReceived -= CaptureFailure;
            }
        }
    }
}
