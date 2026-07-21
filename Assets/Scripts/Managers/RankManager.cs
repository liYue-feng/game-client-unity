// RankManager.cs — 排行榜管理器
//
// 负责排行榜的查询和分数提交。
// 排行榜数据来自 Redis Sorted Set，查询速度快。
//
// 使用方式：
//   // 查询排行榜
//   RankManager.Instance.GetRank(1, 0, 10);
//
//   // 提交分数
//   RankManager.Instance.SubmitScore(9999, "{\"kills\":50,\"time\":180}");

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Network;
using Game.Protocol;
using UnityEngine;

namespace Game.Managers
{
    public class RankManager : MonoBehaviour
    {
        private static RankManager _instance;
        private readonly HashSet<uint> _pendingRequests = new HashSet<uint>();
        private bool _destroyed;
        public static RankManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("[RankManager]");
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<RankManager>();
                }
                return _instance;
            }
        }

        /// <summary>当前排行榜数据</summary>
        public RankItem[] CurrentRanks { get; private set; }

        /// <summary>玩家历史最高分</summary>
        public long BestScore { get; private set; }

        /// <summary>排行榜加载成功事件</summary>
        public event Action<RankItem[]> OnRankLoaded;

        /// <summary>分数提交成功事件</summary>
        public event Action<long> OnScoreSubmitted;

        /// <summary>操作失败事件</summary>
        public event Action<string> OnError;

        /// <summary>排行榜数据接收事件（供 RankPanelUI 等使用）</summary>
        public event Action<RankEntry[]> OnRankDataReceived;

        /// <summary>默认每次拉取数量</summary>
        private const int DefaultFetchCount = 20;

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

        private void OnDestroy()
        {
            _destroyed = true;
            foreach (var seq in new List<uint>(_pendingRequests))
            {
                NetworkClient.Instance.CancelRequest(seq);
            }

            _pendingRequests.Clear();
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 查询排行榜
        /// </summary>
        /// <param name="rankType">排行榜类型：1=最高分 2=击杀数</param>
        /// <param name="start">起始排名（从0开始）</param>
        /// <param name="count">请求数量</param>
        public void GetRank(int rankType, int start, int count)
        {
            Debug.Log($"[RankManager] 查询排行榜: type={rankType} start={start} count={count}");
            RequestRank(rankType, start, count);
        }

        /// <summary>
        /// 提交分数
        /// </summary>
        /// <param name="score">本局分数</param>
        /// <param name="metadata">附加数据（击杀数、存活时间等）</param>
        public void SubmitScore(long score, ScoreMetadata metadata = null)
        {
            Debug.Log($"[RankManager] 提交分数: score={score}");
            Request<SubmitScoreReq, SubmitScoreResp>(
                MsgID.SubmitScoreReq,
                MsgID.SubmitScoreResp,
                new SubmitScoreReq
                {
                    Score = score,
                    Metadata = metadata ?? new ScoreMetadata()
                },
                HandleSubmitScoreResp,
                reason => OnError?.Invoke(reason));
        }

        /// <summary>
        /// 拉取排行榜列表（默认类型1=最高分，前20名）
        /// 供 RankPanelUI 等外部模块使用的便捷方法
        /// </summary>
        public void FetchRankList(int rankType = 1, int start = 0, int count = DefaultFetchCount)
        {
            Debug.Log($"[RankManager] 拉取排行榜: type={rankType} start={start} count={count}");
            RequestRank(rankType, start, count);
        }

        private void RequestRank(int rankType, int start, int count)
        {
            Request<GetRankReq, GetRankResp>(
                MsgID.GetRankReq,
                MsgID.GetRankResp,
                new GetRankReq
                {
                    RankType = rankType,
                    Start = start,
                    Count = count
                },
                HandleGetRankResp,
                reason => OnError?.Invoke(reason));
        }

        private bool Request<TRequest, TResponse>(
            ushort requestId,
            ushort responseId,
            TRequest payload,
            Action<TResponse> onSuccess,
            Action<string> onFailure)
            where TRequest : class, Google.Protobuf.IMessage<TRequest>
            where TResponse : class, Google.Protobuf.IMessage<TResponse>
        {
            var completed = false;
            uint seq = 0;
            var sent = NetworkClient.Instance.Request<TRequest, TResponse>(
                requestId,
                responseId,
                payload,
                response =>
                {
                    completed = true;
                    _pendingRequests.Remove(seq);
                    if (!_destroyed)
                    {
                        onSuccess?.Invoke(response);
                    }
                },
                reason =>
                {
                    completed = true;
                    _pendingRequests.Remove(seq);
                    if (!_destroyed)
                    {
                        onFailure?.Invoke(reason);
                    }
                },
                out seq);
            if (sent && !completed)
            {
                if (_destroyed)
                {
                    NetworkClient.Instance.CancelRequest(seq);
                }
                else
                {
                    _pendingRequests.Add(seq);
                }
            }

            return sent;
        }

        private void HandleGetRankResp(GetRankResp resp)
        {
            CurrentRanks = resp.Ranks.ToArray();
            Debug.Log($"[RankManager] 排行榜加载成功: {CurrentRanks.Length} 条");
            OnRankLoaded?.Invoke(CurrentRanks);

            // 转换为 RankEntry[] 并触发 OnRankDataReceived
            if (CurrentRanks != null)
            {
                var entries = new RankEntry[CurrentRanks.Length];
                for (int i = 0; i < CurrentRanks.Length; i++)
                {
                    var item = CurrentRanks[i];
                    entries[i] = new RankEntry
                    {
                        playerName = item.Nickname,
                        level = item.Level,
                        score = (int)item.Score,
                        rank = item.Rank
                    };
                }
                OnRankDataReceived?.Invoke(entries);
            }
        }

        private void HandleSubmitScoreResp(SubmitScoreResp resp)
        {
            if (resp.Success)
            {
                BestScore = resp.BestScore;
                Debug.Log($"[RankManager] 分数提交成功: best_score={resp.BestScore}");
                OnScoreSubmitted?.Invoke(resp.BestScore);
            }
            else
            {
                OnError?.Invoke("分数提交失败");
            }
        }
    }

    /// <summary>
    /// 排行榜条目（供 UI 显示使用，包含等级信息）
    /// </summary>
    [Serializable]
    public class RankEntry
    {
        public string playerName;
        public int level;
        public int score;
        public int rank;
    }
}
