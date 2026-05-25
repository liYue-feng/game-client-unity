using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 小地图渲染器：将地牢网格渲染为 UI 小方块。
/// 当前房间高亮，已清理房间变暗，未探索房间隐藏。
/// </summary>
public class MinimapRenderer : MonoBehaviour
{
    [Header("小地图参数")]
    [Tooltip="房间方块大小（像素）")]
    public float cellSize = 16f;
    [Tooltip="房间间距")]
    public float cellGap = 2f;
    [Tooltip="当前房间颜色")]
    public Color currentRoomColor = new Color(1f, 0.9f, 0.3f);
    [Tooltip="已清理房间颜色")]
    public Color clearedRoomColor = new Color(0.5f, 0.7f, 0.5f, 0.7f);
    [Tooltip="未清理房间颜色")]
    public Color unclearedRoomColor = new Color(0.8f, 0.4f, 0.3f, 0.8f);
    [Tooltip="Boss房间颜色")]
    public Color bossRoomColor = new Color(0.8f, 0.2f, 0.2f);

    private Dictionary<Vector2Int, GameObject> _roomCells = new Dictionary<Vector2Int, GameObject>();
    private DungeonGrid _grid;

    /// <summary>初始化小地图</summary>
    public void Initialize(DungeonGrid grid)
    {
        _grid = grid;
        ClearCells();

        foreach (var room in grid.Rooms)
        {
            CreateCell(room);
        }

        UpdateDisplay();
    }

    /// <summary>更新显示（房间状态变化后调用）</summary>
    public void UpdateDisplay()
    {
        if (_grid == null) return;

        foreach (var room in _grid.Rooms)
        {
            if (!_roomCells.TryGetValue(room.GridPos, out var cell)) continue;

            var sr = cell.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            // 只显示已访问的房间
            bool visible = room.isVisited;
            cell.SetActive(visible);

            if (!visible) continue;

            // 当前房间
            var currentRoom = DungeonManager.Instance.CurrentRoom;
            if (currentRoom != null && room.GridPos == currentRoom.GridPos)
            {
                sr.color = currentRoomColor;
            }
            else if (room.roomType == RoomType.Boss)
            {
                sr.color = bossRoomColor;
            }
            else if (room.isCleared)
            {
                sr.color = clearedRoomColor;
            }
            else
            {
                sr.color = unclearedRoomColor;
            }
        }
    }

    private void CreateCell(RoomNode room)
    {
        GameObject cell = new GameObject($"Minimap_{room.gridX}_{room.gridY}");
        cell.transform.SetParent(transform);

        // 位置：将网格坐标映射到屏幕空间
        float x = (room.gridX - _grid.width / 2f) * (cellSize + cellGap);
        float y = (room.gridY - _grid.height / 2f) * (cellSize + cellGap);
        cell.transform.localPosition = new Vector3(x, y, 0f);

        var sr = cell.AddComponent<SpriteRenderer>();
        sr.sprite = PlaceholderSpriteFactory.CreateRect(
            Mathf.RoundToInt(cellSize),
            Mathf.RoundToInt(cellSize),
            Color.white
        );
        sr.sortingOrder = 100;

        // 默认隐藏未访问房间
        cell.SetActive(room.isVisited);

        _roomCells[room.GridPos] = cell;
    }

    private void ClearCells()
    {
        foreach (var cell in _roomCells.Values)
        {
            if (cell != null) Destroy(cell);
        }
        _roomCells.Clear();
    }
}
