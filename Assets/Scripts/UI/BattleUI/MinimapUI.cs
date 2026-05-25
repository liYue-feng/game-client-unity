using UnityEngine;

/// <summary>
/// 小地图UI：右上角显示地牢网格。
/// 委托 MinimapRenderer 做渲染，这里只做位置和更新调度。
/// </summary>
public class MinimapUI : MonoBehaviour
{
    [Tooltip("小地图渲染器")]
    public MinimapRenderer minimapRenderer;

    private void Start()
    {
        DungeonManager.Instance.OnRoomCleared += OnRoomCleared;
    }

    private void OnDestroy()
    {
        if (DungeonManager.Instance != null)
            DungeonManager.Instance.OnRoomCleared -= OnRoomCleared;
    }

    /// <summary>初始化小地图（地牢生成后调用）</summary>
    public void InitializeMinimap(DungeonGrid grid)
    {
        if (renderer != null)
        {
            minimapRenderer.Initialize(grid);
        }
    }

    private void OnRoomCleared(RoomNode room)
    {
        if (renderer != null)
        {
            minimapRenderer.UpdateDisplay();
        }
    }
}
