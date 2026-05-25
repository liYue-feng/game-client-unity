using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 耐力条：水墨风格，花青色，低耐力闪烁。
/// 订阅 CharacterStats.OnStaminaChanged 事件更新显示。
/// </summary>
public class StaminaBar : MonoBehaviour
{
    [Tooltip("耐力条滑块")]
    public Slider staminaSlider;
    [Tooltip("填充图片")]
    public Image fillImage;
    [Tooltip("警告闪烁速度")]
    public float flashSpeed = 3f;

    private CharacterStats _stats;
    private bool _isWarning;

    public void Initialize(CharacterStats stats)
    {
        _stats = stats;
        _stats.OnStaminaChanged += UpdateBar;
        UpdateBar(_stats.currentStamina, _stats.maxStamina);
    }

    private void UpdateBar(int current, int max)
    {
        if (staminaSlider == null) return;

        staminaSlider.maxValue = max;
        staminaSlider.value = current;

        float percent = (float)current / max;
        _isWarning = percent < 0.25f;

        if (fillImage != null)
        {
            fillImage.color = _isWarning
                ? ShuiMoPalette.CinnabarRed  // 警告时用朱砂红
                : ShuiMoPalette.FlowerBlue;  // 正常花青色
        }
    }

    private void Update()
    {
        // 低耐力闪烁
        if (_isWarning && fillImage != null)
        {
            float alpha = Mathf.PingPong(Time.time * flashSpeed, 1f);
            Color c = fillImage.color;
            c.a = 0.4f + alpha * 0.6f;
            fillImage.color = c;
        }
        else if (fillImage != null)
        {
            Color c = fillImage.color;
            c.a = 1f;
            fillImage.color = c;
        }
    }

    private void OnDestroy()
    {
        if (_stats != null)
            _stats.OnStaminaChanged -= UpdateBar;
    }
}
