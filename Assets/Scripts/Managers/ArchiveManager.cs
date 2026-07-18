// ArchiveManager.cs — 存档管理器
//
// 负责游戏存档的保存和加载。
// 吸血鬼幸存者类游戏的存档包含：解锁的角色/武器、历史最高分、金币等。
//
// 存档策略：
//   - 游戏过程中的实时状态由客户端维护（不频繁写服务器）
//   - 关键节点保存到服务器：每局结束时、退出游戏时、手动保存时
//   - 上线时从服务器加载最新存档
//
// 为什么不在每次状态变化时都保存？
//   - 网络延迟：每次保存都要等服务器响应
//   - 服务器压力：大量玩家频繁保存会打爆数据库
//   - 游戏体验：保存是低优先级操作，不应影响游戏流畅度

using System;
using System.Collections.Generic;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Managers
{
    public class ArchiveManager : MonoBehaviour
    {
        private static ArchiveManager _instance;
        private readonly List<IDisposable> _networkSubscriptions = new List<IDisposable>();
        public static ArchiveManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[ArchiveManager]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<ArchiveManager>();
                }
                return _instance;
            }
        }

        /// <summary>当前存档数据（JSON字符串）</summary>
        public string CurrentData { get; private set; }

        /// <summary>存档加载成功事件</summary>
        public event Action<string> OnLoadSuccess;

        /// <summary>存档保存成功事件</summary>
        public event Action OnSaveSuccess;

        /// <summary>存档操作失败事件</summary>
        public event Action<string> OnError;

        /// <summary>是否正在保存（防止重复保存）</summary>
        private bool _isSaving;

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
            _networkSubscriptions.Add(client.On<SaveArchiveResp>(MsgID.SaveArchiveResp, HandleSaveResp));
            _networkSubscriptions.Add(client.On<LoadArchiveResp>(MsgID.LoadArchiveResp, HandleLoadResp));
        }

        private void OnDestroy()
        {
            foreach (var subscription in _networkSubscriptions)
            {
                subscription.Dispose();
            }

            _networkSubscriptions.Clear();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 从服务器加载存档
        /// 通常在登录成功后调用
        /// </summary>
        public void LoadArchive()
        {
            if (!NetworkClient.Instance.IsLoggedIn)
            {
                OnError?.Invoke("未登录，无法加载存档");
                return;
            }

            Debug.Log("[ArchiveManager] 加载存档...");
            NetworkClient.Instance.Send(MsgID.LoadArchiveReq, new LoadArchiveReq());
        }

        /// <summary>
        /// 保存存档到服务器
        ///
        /// 参数：
        ///   data - 存档数据的 JSON 字符串
        ///   immediate - 是否立即保存（退出游戏时为 true）
        /// </summary>
        public void SaveArchive(string data, bool immediate = false)
        {
            if (!NetworkClient.Instance.IsLoggedIn)
            {
                OnError?.Invoke("未登录，无法保存存档");
                return;
            }

            if (_isSaving)
            {
                Debug.Log("[ArchiveManager] 正在保存中，跳过");
                return;
            }

            _isSaving = true;
            CurrentData = data;
            Debug.Log($"[ArchiveManager] 保存存档... (dataLen={data.Length})");

            NetworkClient.Instance.Send(MsgID.SaveArchiveReq, new SaveArchiveReq { data = data });
        }

        /// <summary>
        /// 处理保存响应
        /// </summary>
        private void HandleSaveResp(SaveArchiveResp resp)
        {
            _isSaving = false;

            if (resp.success)
            {
                Debug.Log("[ArchiveManager] 存档保存成功");
                OnSaveSuccess?.Invoke();
            }
            else
            {
                Debug.LogError("[ArchiveManager] 存档保存失败");
                OnError?.Invoke("存档保存失败");
            }
        }

        /// <summary>
        /// 处理加载响应
        /// </summary>
        private void HandleLoadResp(LoadArchiveResp resp)
        {
            CurrentData = resp.data;

            if (string.IsNullOrEmpty(resp.data))
            {
                Debug.Log("[ArchiveManager] 新玩家，无存档");
            }
            else
            {
                Debug.Log($"[ArchiveManager] 存档加载成功 (dataLen={resp.data.Length})");
            }

            OnLoadSuccess?.Invoke(resp.data);
        }

        /// <summary>
        /// 应用退出时自动保存
        /// </summary>
        private void OnApplicationQuit()
        {
            if (!string.IsNullOrEmpty(CurrentData) && NetworkClient.Instance.IsLoggedIn)
            {
                // 退出时同步保存（不等响应）
                NetworkClient.Instance.Send(MsgID.SaveArchiveReq, new SaveArchiveReq { data = CurrentData });
            }
        }

        /// <summary>
        /// 从指定槽位加载存档（兼容代码）
        /// </summary>
        public void LoadArchive(int slotIndex)
        {
            Debug.Log($"[ArchiveManager] 加载存档槽 {slotIndex}...");
            LoadArchive(); // 简单地使用现有的加载逻辑
        }

        /// <summary>
        /// 删除指定槽位的存档
        /// </summary>
        public void DeleteArchive(int slotIndex)
        {
            Debug.Log($"[ArchiveManager] 删除存档槽 {slotIndex}...");
            // 简单实现：发送空存档覆盖
            SaveArchive("");
        }
    }
}
