using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 房间通关横幅："房间已清！"动画 + 得分显示。
/// </summary>
public class RoomClearBanner : MonoBehaviour
{
    [Tooltip="横幅文字")]
    public Text bannerText;
    [Tooltip="得分文字")]
    public Text scoreText;
    [Tooltip="显示持续时间")]
    public float displayDuration = 1.5f;

    private void Start()
    {
        DungeonManager.Instance.OnRoomCleared += OnRoomCleared;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (DungeonManager.Instance != null)
            DungeonManager.Instance.OnRoomCleared -= OnRoomCleared;
    }

    private void OnRoomCleared(RoomNode room)
    {
        gameObject.SetActive(true);

        if (bannerText != null) bannerText.text = "房间已清！";
        if (scoreText != null) scoreText.text = "+100";

        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        gameObject.SetActive(false);
    }
}
