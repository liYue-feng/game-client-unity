using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 房间类型枚举
/// </summary>
public enum RoomType
{
    /// <summary>起始房间</summary>
    Start,
    /// <summary>战斗房间（波次刷怪）</summary>
    Combat,
    /// <summary>奖励房间（3选1）</summary>
    Reward,
    /// <summary>休息房间（恢复+升级）</summary>
    Rest,
    /// <summary>Boss房间</summary>
    Boss
}

/// <summary>
/// 房间节点：地牢网格中的一个格子。
/// 包含房间类型、位置、出口连接、难度等信息。
/// 由 DungeonGrid 生成，由 RoomGenerator 读取构建场景。
/// </summary>
public class RoomNode
{
    /// <summary>房间类型</summary>
    public RoomType roomType;
    /// <summary>网格X坐标</summary>
    public int gridX;
    /// <summary>网格Y坐标</summary>
    public int gridY;
    /// <summary>出口连接：[上, 右, 下, 左]，true=有出口</summary>
    public bool[] exits = new bool[4];
    /// <summary>是否已清理</summary>
    public bool isCleared;
    /// <summary>难度系数（影响敌人数量和强度）</summary>
    public int difficulty;
    /// <summary>是否已被访问</summary>
    public bool isVisited;

    /// <summary>网格坐标</summary>
    public Vector2Int GridPos => new Vector2Int(gridX, gridY);

    /// <summary>出口方向索引：上0 右1 下2 左3</summary>
    public static readonly Vector2Int[] Directions = {
        Vector2Int.up,    // 0=上
        Vector2Int.right, // 1=右
        Vector2Int.down,  // 2=下
        Vector2Int.left   // 3=左
    };

    /// <summary>获取反方向索引</summary>
    public static int Opposite(int dir) => (dir + 2) % 4;

    public RoomNode(int x, int y, RoomType type = RoomType.Combat)
    {
        gridX = x;
        gridY = y;
        roomType = type;
        isCleared = type == RoomType.Start; // 起始房间默认已清
    }
}
