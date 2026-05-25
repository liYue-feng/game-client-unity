using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 流派指示器：5个流派图标，当前流派高亮+资源条。
/// 按1-5键或UI按钮可切换流派。
/// </summary>
public class StyleIndicator : MonoBehaviour
{
    [Tooltip="5个流派图标")]
    public Image[] styleIcons = new Image[5];
    [Tooltip="当前流派高亮颜色")]
    public Color activeColor = new Color(1f, 0.9f, 0.3f);
    [Tooltip="非当前流派颜色")]
    public Color inactiveColor = new Color(0.4f, 0.4f, 0.4f, 0.6f);
    [Tooltip="特殊资源滑块")]
    public Slider resourceSlider;

    private void Start()
    {
        StyleManager.Instance.OnStyleChanged += OnStyleChanged;
        StyleManager.Instance.OnSpecialResourceChanged += OnResourceChanged;
        UpdateDisplay(StyleManager.Instance.CurrentStyleID);
    }

    private void OnDestroy()
    {
        if (StyleManager.Instance != null)
        {
            StyleManager.Instance.OnStyleChanged -= OnStyleChanged;
            StyleManager.Instance.OnSpecialResourceChanged -= OnResourceChanged;
        }
    }

    private void OnStyleChanged(CombatStyleID newStyle)
    {
        UpdateDisplay(newStyle);
    }

    private void OnResourceChanged(int current, int max)
    {
        if (resourceSlider != null)
        {
            resourceSlider.maxValue = max;
            resourceSlider.value = current;
        }
    }

    private void UpdateDisplay(CombatStyleID currentStyle)
    {
        for (int i = 0; i < styleIcons.Length; i++)
        {
            if (styleIcons[i] == null) continue;
            CombatStyleID styleId = (CombatStyleID)(i + 1);
            styleIcons[i].color = styleId == currentStyle ? activeColor : inactiveColor;
        }

        if (resourceSlider != null)
        {
            var data = StyleManager.Instance.CurrentStyleData;
            resourceSlider.maxValue = data.specialResourceMax;
            resourceSlider.value = StyleManager.Instance.SpecialResource;
        }
    }
}
