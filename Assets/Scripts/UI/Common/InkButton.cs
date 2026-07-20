using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 水墨风格按钮 — 配合 Unity Button 使用，自动创建水墨纹理背景和书法文字。
/// 用法: go.AddComponent<InkButton>().buttonText = "开始游戏";
///       go.GetComponent<Button>().onClick.AddListener(() => ...);
/// </summary>
[RequireComponent(typeof(Button), typeof(RectTransform))]
public class InkButton : MonoBehaviour
{
    [Tooltip("按钮显示文字")]
    public string buttonText = "";
    [Tooltip("文字大小")]
    public int fontSize = 28;
    [Tooltip("墨水颜色")]
    public Color inkColor = new Color(0.1f, 0.1f, 0.1f, 1f);

    private Text _label;
    private RawImage _bg;

    void Awake()
    {
        SetupBackground();
        SetupLabel();
    }

    void OnValidate()
    {
        if (_label != null && buttonText != _label.text)
            _label.text = buttonText;
        if (_label != null && _label.fontSize != fontSize)
            _label.fontSize = fontSize;
        if (_label != null && _label.color != inkColor)
            _label.color = inkColor;
    }

    void SetupBackground()
    {
        var existingBg = GetComponent<RawImage>();
        if (existingBg == null)
            _bg = gameObject.AddComponent<RawImage>();
        else
            _bg = existingBg;

        var rt = GetComponent<RectTransform>();
        if (rt.sizeDelta == Vector2.zero)
            rt.sizeDelta = new Vector2(240, 60);

        _bg.texture = CreateInkButtonTex((int)rt.sizeDelta.x, (int)rt.sizeDelta.y);
    }

    void SetupLabel()
    {
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(transform, false);

        _label = labelObj.AddComponent<Text>();
        _label.text = buttonText;
        _label.fontSize = fontSize;
        _label.alignment = TextAnchor.MiddleCenter;
        _label.color = inkColor;
        _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.raycastTarget = false;

        var lrt = labelObj.GetComponent<RectTransform>();
        if (lrt == null)
            lrt = labelObj.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;
    }

    Texture2D CreateInkButtonTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var paperColor = new Color32(245, 240, 232, 255);
        var inkBorder = new Color32(26, 26, 26, 220);
        var inkHover = new Color32(60, 55, 50, 255);
        int borderW = 4;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int idx = y * w + x;
                int borderDist = Mathf.Min(
                    Mathf.Min(x, w - 1 - x),
                    Mathf.Min(y, h - 1 - y)
                );

                if (borderDist < borderW)
                {
                    float noise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                    float t = noise > 0.35f ? 1f : 0.4f;
                    colors[idx] = Color32.Lerp(paperColor, inkBorder, (byte)(t * 255));
                }
                else if (borderDist < borderW + 12)
                {
                    float fade = 1f - (borderDist - borderW) / 12f;
                    colors[idx] = Color32.Lerp(paperColor, inkHover, (byte)(fade * 255));
                }
                else
                {
                    float noise = Mathf.PerlinNoise(x * 0.06f, y * 0.06f);
                    byte grain = (byte)(noise * 15);
                    colors[idx] = new Color32(
                        (byte)(paperColor.r - grain),
                        (byte)(paperColor.g - grain),
                        (byte)(paperColor.b - grain),
                        255);
                }
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();
        return tex;
    }

    void OnDestroy()
    {
        if (_bg != null && _bg.texture != null)
            Destroy(_bg.texture);
    }
}
