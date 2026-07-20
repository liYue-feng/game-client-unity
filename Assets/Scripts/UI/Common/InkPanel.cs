using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 水墨风格面板背景 — 宣纸纹理 + 毛笔边框
/// </summary>
public class InkPanel : MonoBehaviour
{
    public int panelWidth = 600;
    public int panelHeight = 400;
    public int borderSize = 6;

    private RawImage _bg;
    private Texture2D _ownedTexture;

    void Awake()
    {
        Configure(panelWidth, panelHeight);
    }

    public void Configure(int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, "Panel width must be positive.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, "Panel height must be positive.");
        }

        panelWidth = width;
        panelHeight = height;

        var rect = GetComponent<RectTransform>();
        if (rect == null) { rect = gameObject.AddComponent<RectTransform>(); }
        rect.sizeDelta = new Vector2(panelWidth, panelHeight);

        if (_bg == null)
        {
            _bg = GetComponent<RawImage>();
            if (_bg == null)
            {
                _bg = gameObject.AddComponent<RawImage>();
            }
        }

        var oldOwnedTexture = _ownedTexture;
        _ownedTexture = CreatePanelTex(panelWidth, panelHeight);
        _bg.texture = _ownedTexture;
        if (oldOwnedTexture != null)
        {
            Destroy(oldOwnedTexture);
        }
    }

    Texture2D CreatePanelTex(int w, int h)
    {
        var tex = new Texture2D(w, h);
        var colors = new Color32[w * h];
        var paper = new Color32(245, 240, 232, 255);
        var inkBorder = new Color32(26, 26, 26, 200);
        var inkFade = new Color32(220, 215, 205, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var idx = y * w + x;
                var borderDist = Mathf.Min(
                    Mathf.Min(x, w - 1 - x),
                    Mathf.Min(y, h - 1 - y)
                );

                if (borderDist < borderSize)
                {
                    var noise = Mathf.PerlinNoise(x * 0.25f, y * 0.25f);
                    var t = noise > 0.3f ? 1f : 0.3f;
                    colors[idx] = Color32.Lerp(paper, inkBorder, (byte)(t * 255));
                }
                else if (borderDist < borderSize + 20)
                {
                    // 墨晕渐隐
                    var fade = 1f - (borderDist - borderSize) / 20f;
                    colors[idx] = Color32.Lerp(paper, inkFade, (byte)(fade * 255));
                }
                else
                {
                    // 宣纸纹理
                    var texNoise = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    var grain = (byte)(texNoise * 20);
                    colors[idx] = new Color32((byte)(paper.r - grain), (byte)(paper.g - grain), (byte)(paper.b - grain), 255);
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
        if (_ownedTexture == null)
        {
            _bg = null;
            return;
        }

        if (_bg != null && _bg.texture == _ownedTexture)
        {
            _bg.texture = null;
        }

        var ownedTexture = _ownedTexture;
        _ownedTexture = null;
        Destroy(ownedTexture);
        _bg = null;
    }
}
