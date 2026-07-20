using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 地牢管理器：管理当前地牢运行的全局状态。
/// 单例模式，在进入地牢时创建，退出时销毁。
///
/// 生命周期：
/// StartDungeon(level) → 生成网格 → 加载起始房间 →
/// 逐房间推进 → Boss击杀 → OnDungeonComplete() → 上报服务器
/// </summary>
public class DungeonManager : MonoBehaviour
{
    private static DungeonManager _instance;
    public static DungeonManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[DungeonManager]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<DungeonManager>();
            }
            return _instance;
        }
    }

    /// <summary>当前地牢网格</summary>
    public DungeonGrid CurrentDungeon { get; private set; }
    /// <summary>当前所在房间</summary>
    public RoomNode CurrentRoom { get; private set; }
    /// <summary>地牢等级</summary>
    public int DungeonLevel { get; private set; }

    // 运行统计
    private int _roomsCleared;
    private int _totalKills;
    private int _totalScore;
    private float _startTime;

    /// <summary>房间清理完成事件</summary>
    public event System.Action<RoomNode> OnRoomCleared;
    /// <summary>地牢通关事件</summary>
    public event System.Action<int, int, int, float> OnDungeonComplete; // score, kills, roomsCleared, time

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 监听敌人死亡
        CombatEvents.OnEnemyDeath += OnEnemyDeathHandler;
    }

    private void OnDestroy()
    {
        CombatEvents.OnEnemyDeath -= OnEnemyDeathHandler;
    }

    /// <summary>开始地牢运行</summary>
    public void StartDungeon(int level)
    {
        DungeonLevel = level;
        _roomsCleared = 0;
        _totalKills = 0;
        _totalScore = 0;
        _startTime = Time.time;

        // 生成地牢
        CurrentDungeon = new DungeonGrid();
        CurrentDungeon.Generate(level);

        // 加载起始房间
        CurrentRoom = CurrentDungeon.StartRoom;
        CurrentRoom.isVisited = true;

        LoadRoom(CurrentRoom);
    }

    /// <summary>加载指定房间</summary>
    public void LoadRoom(RoomNode room)
    {
        CurrentRoom = room;
        room.isVisited = true;

        // 根据房间类型执行不同逻辑
        switch (room.roomType)
        {
            case RoomType.Start:
                // 起始房间，直接标记为已清
                MarkRoomCleared();
                break;
            case RoomType.Combat:
                // 战斗房间，由 WaveSpawner 处理
                break;
            case RoomType.Boss:
                // Boss房间
                break;
            case RoomType.Reward:
                // 奖励房间
                break;
            case RoomType.Rest:
                // 休息房间
                break;
        }
    }

    /// <summary>标记当前房间已清理</summary>
    public void MarkRoomCleared()
    {
        if (CurrentRoom.isCleared) return;

        CurrentRoom.isCleared = true;
        _roomsCleared++;
        OnRoomCleared?.Invoke(CurrentRoom);

        // Boss击杀 → 地牢通关
        if (CurrentRoom.roomType == RoomType.Boss)
        {
            CompleteDungeon();
        }
    }

    /// <summary>获取与当前房间连接的相邻房间</summary>
    public RoomNode GetAdjacentRoom(int direction)
    {
        if (CurrentRoom == null || !CurrentRoom.exits[direction]) return null;

        Vector2Int adjPos = CurrentRoom.GridPos + RoomNode.Directions[direction];
        return CurrentDungeon.GetRoom(adjPos);
    }

    /// <summary>地牢通关</summary>
    private void CompleteDungeon()
    {
        float survivalTime = Time.time - _startTime;
        OnDungeonComplete?.Invoke(_totalScore, _totalKills, _roomsCleared, survivalTime);
    }

    private void OnEnemyDeathHandler(GameObject enemy)
    {
        _totalKills++;
        _totalScore += 100; // 基础击杀分
    }
}
