using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 水墨风格输入框
/// </summary>
public class InkInputField : MonoBehaviour
{
    public string placeholderText = "";
    public int fontSize = 28;

    private InputField _inputField;
    private RawImage _bg;
    private Text _placeholderLabel;

    void Awake()
    {
        BuildInputField();
    }

    void BuildInputField()
    {
        var rect = GetComponent<RectTransform>();
        if (rect == null) { rect = gameObject.AddComponent<RectTransform>(); }
        rect.sizeDelta = new Vector2(300, 50);

        // 背景
        var bgGo = new GameObject("BG");
        bgGo.transform.SetParent(transform, false);
        _bg = bgGo.AddComponent<RawImage>();
        _bg.texture = CreateInputTex(300, 50);
        _bg.rectTransform.sizeDelta = new Vector2(300, 50);

        // 占位文字
        var phGo = new GameObject("Placeholder");
        phGo.transform.SetParent(transform, false);
        _placeholderLabel = phGo.AddComponent<Text>();
        _placeholderLabel.text = placeholderText;
        _placeholderLabel.fontSize = fontSize;
        _placeholderLabel.alignment = TextAnchor.MiddleLeft;
        _placeholderLabel.color = new Color(0.5f, 0.45f, 0.4f);
        _placeholderLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _placeholderLabel.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 10, 280);
        _placeholderLabel.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 50);

        // 输入文字
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(transform, false);
        var textLabel = textGo.AddComponent<Text>();
        textLabel.fontSize = fontSize;
        textLabel.alignment = TextAnchor.MiddleLeft;
        textLabel.color = new Color(0.1f, 0.1f, 0.1f);
        textLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textLabel.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 10, 280);
        textLabel.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, 50);

        // InputField
        _inputField = gameObject.AddComponent<InputField>();
        _inputField.targetGraphic = _bg;
        _inputField.textComponent = textLabel;
        _inputField.placeholder = _placeholderLabel;
    }

    Texture2D CreateInputTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var paper = new Color32(245, 240, 232, 255);
        var ink = new Color32(80, 70, 60, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = y * w + x;
                // 底部墨线
                var isBottomLine = y >= h - 3;
                var noise = Mathf.PerlinNoise(x * 0.2f, y * 0.3f);
                var alpha = isBottomLine ? (noise > 0.35f ? 180 : 40) : 0;
                colors[idx] = Color32.Lerp(paper, ink, (byte)alpha);
            }
        }
        tex.SetPixels32(colors);
        tex.filterMode = FilterMode.Point;
        tex.Apply();
        return tex;
    }

    public string text
    {
        get => _inputField != null ? _inputField.text : "";
        set { if (_inputField != null) _inputField.text = value; }
    }

    public InputField.ContentType contentType
    {
        get => _inputField.contentType;
        set => _inputField.contentType = value;
    }

    void OnDestroy()
    {
        if (_bg != null && _bg.texture != null)
            Destroy(_bg.texture);
    }
}