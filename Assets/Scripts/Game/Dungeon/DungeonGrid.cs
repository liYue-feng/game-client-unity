using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地牢网格：生成和管理地牢的房间布局。
///
/// 生成算法：
/// 1. 中心放起始房间
/// 2. 从起始房间随机游走8-12步，放置房间
/// 3. 最远端放Boss房间
/// 4. 最长路径上放1-2个休息房间
/// 5. 其余房间随机分配战斗/奖励
///
/// 为什么用网格而不是自由放置：
/// - 横版2D游戏，房间之间的左右/上下关系很直观
/// - 网格便于生成小地图
/// - 简单可靠，不会生成不可达的房间
/// </summary>
public class DungeonGrid
{
    /// <summary>网格宽高</summary>
    public int width = 5;
    public int height = 5;
    /// <summary>地牢等级</summary>
    public int level = 1;

    /// <summary>所有房间节点，key=gridPos</summary>
    private Dictionary<Vector2Int, RoomNode> _rooms = new Dictionary<Vector2Int, RoomNode>();
    /// <summary>房间列表（有序）</summary>
    private List<RoomNode> _roomList = new List<RoomNode>();

    /// <summary>所有房间</summary>
    public IReadOnlyList<RoomNode> Rooms => _roomList;
    /// <summary>房间数量</summary>
    public int RoomCount => _roomList.Count;
    /// <summary>起始房间</summary>
    public RoomNode StartRoom { get; private set; }
    /// <summary>Boss房间</summary>
    public RoomNode BossRoom { get; private set; }

    /// <summary>
    /// 生成地牢
    /// </summary>
    /// <param name="dungeonLevel">地牢等级，影响房间数量和难度</param>
    public void Generate(int dungeonLevel)
    {
        level = dungeonLevel;
        _rooms.Clear();
        _roomList.Clear();

        // 1. 中心放起始房间
        Vector2Int center = new Vector2Int(width / 2, height / 2);
        StartRoom = new RoomNode(center.x, center.y, RoomType.Start);
        AddRoom(StartRoom);

        // 2. 随机游走生成房间
        int targetRooms = 8 + dungeonLevel * 2;
        targetRooms = Mathf.Min(targetRooms, width * height);
        targetRooms = Mathf.Max(targetRooms, 6);

        Vector2Int currentPos = center;
        List<Vector2Int> path = new List<Vector2Int> { center };

        for (int i = 1; i < targetRooms; i++)
        {
            // 尝试随机方向走一步
            List<Vector2Int> validDirs = new List<Vector2Int>();
            for (int d = 0; d < 4; d++)
            {
                Vector2Int next = currentPos + RoomNode.Directions[d];
                if (IsValidPos(next) && !HasRoom(next))
                {
                    validDirs.Add(next);
                }
            }

            if (validDirs.Count == 0)
            {
                // 死胡同，回退到有可用方向的房间
                currentPos = FindRoomWithOpenNeighbor();
                if (currentPos == new Vector2Int(-1, -1)) break;
                continue;
            }

            Vector2Int chosen = validDirs[Random.Range(0, validDirs.Count)];
            int dir = GetDirection(currentPos, chosen);

            // 创建房间
            var room = new RoomNode(chosen.x, chosen.y, RoomType.Combat);
            room.difficulty = dungeonLevel;
            AddRoom(room);

            // 连接出口
            ConnectRooms(GetRoom(currentPos), room, dir);

            path.Add(chosen);
            currentPos = chosen;
        }

        // 3. 最远端放Boss
        BossRoom = FindFurthestRoom(StartRoom);
        if (BossRoom != null)
        {
            BossRoom.roomType = RoomType.Boss;
            BossRoom.difficulty = dungeonLevel + 1;
        }

        // 4. 路径上放休息房间
        PlaceRestRooms();

        // 5. 其余非战斗房间
        PlaceRewardRooms();
    }

    /// <summary>获取指定位置的房间</summary>
    public RoomNode GetRoom(Vector2Int pos)
    {
        _rooms.TryGetValue(pos, out var room);
        return room;
    }

    /// <summary>是否有指定位置的房间</summary>
    public bool HasRoom(Vector2Int pos) => _rooms.ContainsKey(pos);

    private void AddRoom(RoomNode room)
    {
        _rooms[room.GridPos] = room;
        _roomList.Add(room);
    }

    private void ConnectRooms(RoomNode a, RoomNode b, int dir)
    {
        if (a == null || b == null) return;
        a.exits[dir] = true;
        b.exits[RoomNode.Opposite(dir)] = true;
    }

    private bool IsValidPos(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }

    private int GetDirection(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        for (int d = 0; d < 4; d++)
        {
            if (diff == RoomNode.Directions[d]) return d;
        }
        return -1;
    }

    /// <summary>找到距离起点最远的房间</summary>
    private RoomNode FindFurthestRoom(RoomNode start)
    {
        RoomNode furthest = null;
        int maxDist = 0;

        foreach (var room in _roomList)
        {
            int dist = Mathf.Abs(room.gridX - start.gridX) + Mathf.Abs(room.gridY - start.gridY);
            if (dist > maxDist && room != start)
            {
                maxDist = dist;
                furthest = room;
            }
        }
        return furthest;
    }

    /// <summary>找到还有空位可以扩展的房间位置</summary>
    private Vector2Int FindRoomWithOpenNeighbor()
    {
        // 从已有房间中随机选一个检查
        var shuffled = new List<RoomNode>(_roomList);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        foreach (var room in shuffled)
        {
            for (int d = 0; d < 4; d++)
            {
                Vector2Int next = room.GridPos + RoomNode.Directions[d];
                if (IsValidPos(next) && !HasRoom(next))
                {
                    return room.GridPos;
                }
            }
        }
        return new Vector2Int(-1, -1);
    }

    /// <summary>放置休息房间</summary>
    private void PlaceRestRooms()
    {
        int restCount = Mathf.Max(1, _roomList.Count / 5);
        List<RoomNode> candidates = new List<RoomNode>();

        foreach (var room in _roomList)
        {
            if (room.roomType == RoomType.Combat)
            {
                // 选择离起点有一定距离的战斗房间
                int dist = Mathf.Abs(room.gridX - StartRoom.gridX) + Mathf.Abs(room.gridY - StartRoom.gridY);
                if (dist >= 2) candidates.Add(room);
            }
        }

        for (int i = 0; i < restCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            candidates[idx].roomType = RoomType.Rest;
            candidates.RemoveAt(idx);
        }
    }

    /// <summary>放置奖励房间</summary>
    private void PlaceRewardRooms()
    {
        int rewardCount = Mathf.Max(1, _roomList.Count / 6);
        List<RoomNode> candidates = new List<RoomNode>();

        foreach (var room in _roomList)
        {
            if (room.roomType == RoomType.Combat) candidates.Add(room);
        }

        for (int i = 0; i < rewardCount && candidates.Count > 0; i++)
        {
            int idx = Random.Range(0, candidates.Count);
            candidates[idx].roomType = RoomType.Reward;
            candidates.RemoveAt(idx);
        }
    }
}
