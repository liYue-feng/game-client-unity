using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 玩家血条：水墨风格，朱砂红渐变。
/// 订阅 CharacterStats.OnHpChanged 事件更新显示。
/// </summary>
public class PlayerHPBar : MonoBehaviour
{
    [Tooltip("血条滑块")]
    public Slider hpSlider;
    [Tooltip("填充图片")]
    public Image fillImage;
    [Tooltip("背景图片")]
    public Image backgroundImage;
    [Tooltip("边框图片")]
    public Image borderImage;

    private CharacterStats _stats;

    public void Initialize(CharacterStats stats)
    {
        _stats = stats;
        _stats.OnHpChanged += UpdateBar;
        UpdateBar(_stats.currentHp, _stats.maxHp);
    }

    private void UpdateBar(int current, int max)
    {
        if (hpSlider == null) return;

        hpSlider.maxValue = max;
        hpSlider.value = current;

        if (fillImage != null)
        {
            // 水墨渐变：满血时用朱砂红，低血时加重墨色
            float percent = (float)current / max;
            if (percent > 0.6f)
                fillImage.color = ShuiMoPalette.CinnabarRed;
            else if (percent > 0.3f)
                fillImage.color = ShuiMoPalette.Interpolate(ShuiMoPalette.CinnabarRed, ShuiMoPalette.ThickInk, 0.3f);
            else
                fillImage.color = ShuiMoPalette.Interpolate(ShuiMoPalette.CinnabarRed, ShuiMoPalette.ThickInk, 0.6f);
        }
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnHpChanged -= UpdateBar;
    }
}
