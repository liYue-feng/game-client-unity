// MainThreadDispatcher.cs — 主线程调度器
//
// WebSocket 的消息回调在工作线程中执行，但 Unity 的 API（如 Transform、UI 等）
// 只能在主线程中使用。这个组件负责将工作线程的任务调度到主线程执行。
//
// 原理：
//   1. 工作线程通过 Enqueue() 将 Action 放入队列
//   2. Unity 的 Update() 在主线程中执行，每帧检查队列
//   3. 取出并执行所有排队的 Action
//
// 这是 Unity + WebSocket 的标准解决方案，几乎所有 WebSocket 库都需要。
//
// 使用方式：
//   // 在工作线程中
//   MainThreadDispatcher.Enqueue(() => {
//       // 这里的代码会在主线程中执行
//       transform.position = Vector3.zero;
//   });

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Network
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static readonly object _lock = new object();

        /// <summary>
        /// 获取单例，不存在则自动创建
        /// </summary>
        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[MainThreadDispatcher]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MainThreadDispatcher>();
                }
                return _instance;
            }
        }

        private void Awake()
        {
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
            // 每帧处理所有排队的 Action
            // 为什么要锁？因为 Enqueue 可能从工作线程调用，而这里在主线程
            lock (_lock)
            {
                while (_queue.Count > 0)
                {
                    var action = _queue.Dequeue();
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[MainThreadDispatcher] 执行异常: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 将 Action 排队到主线程执行
        /// 线程安全：可以从任何线程调用
        /// </summary>
        public static void Enqueue(Action action)
        {
            lock (_lock)
            {
                _queue.Enqueue(action);
            }
        }
    }
}
