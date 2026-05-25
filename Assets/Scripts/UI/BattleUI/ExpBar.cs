using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 经验条UI：水墨风格，显示当前等级和经验进度。
/// 升级时播放动画效果。
/// </summary>
public class ExpBar : MonoBehaviour
{
    [Tooltip("经验条滑块")]
    public Slider expSlider;
    [Tooltip("填充图片")]
    public Image fillImage;
    [Tooltip("等级文字")]
    public Text levelText;

    private CharacterStats _stats;

    public void Initialize(CharacterStats stats)
    {
        _stats = stats;
        _stats.OnExpChanged += UpdateDisplay;
        _stats.OnLevelUp += OnLevelUp;
        UpdateDisplay(stats.currentExp, stats.ExpToNextLevel);
    }

    private void UpdateDisplay(int current, int toNext)
    {
        if (expSlider != null)
        {
            expSlider.maxValue = toNext;
            expSlider.value = current;
        }

        if (levelText != null && _stats != null)
        {
            levelText.text = $"Lv.{_stats.level}";
        }
    }

    private void OnLevelUp(int newLevel)
    {
        // 升级时播放简单的闪烁效果
        if (fillImage != null)
        {
            StartCoroutine(LevelUpFlashCoroutine());
        }
    }

    private System.Collections.IEnumerator LevelUpFlashCoroutine()
    {
        Color original = fillImage.color;
        for (int i = 0; i < 4; i++)
        {
            fillImage.color = ShuiMoPalette.Gamboge;
            yield return new WaitForSeconds(0.1f);
            fillImage.color = original;
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void OnDestroy()
    {
        if (_stats != null)
        {
            _stats.OnExpChanged -= UpdateDisplay;
            _stats.OnLevelUp -= OnLevelUp;
        }
    }
}
