using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 地牢结算画面：显示本局得分/击杀/时间/流派，上报服务器。
/// </summary>
public class DungeonResultScreen : MonoBehaviour
{
    [Header("UI元素")]
    public Text scoreText;
    public Text killsText;
    public Text timeText;
    public Text styleText;
    public Button continueButton;

    private void Start()
    {
        DungeonManager.Instance.OnDungeonComplete += OnDungeonComplete;
        gameObject.SetActive(false);

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinue);
        }
    }

    private void OnDestroy()
    {
        if (DungeonManager.Instance != null)
            DungeonManager.Instance.OnDungeonComplete -= OnDungeonComplete;
    }

    private void OnDungeonComplete(int score, int kills, int roomsCleared, float time)
    {
        gameObject.SetActive(true);

        if (scoreText != null) scoreText.text = $"得分: {score}";
        if (killsText != null) killsText.text = $"击杀: {kills}";
        if (timeText != null) timeText.text = $"时间: {time:F1}s";
        if (styleText != null) styleText.text = $"流派: {StyleManager.Instance.CurrentStyleData.styleName}";
    }

    private void OnContinue()
    {
        gameObject.SetActive(false);
        // TODO: 返回主菜单或重新开始
    }
}
