using UnityEngine;

/// <summary>
/// 占位精灵工厂：运行时用代码生成水墨画风格的精灵。
/// 使用 shuimo-ui 配色方案，模拟毛笔笔触和宣纸质感。
/// 生成方式：Texture2D → Sprite.Create，无需任何图片文件。
///
/// 参考：shuimo-ui (janghood/shuimo-ui) 设计系统
/// </summary>
public static class PlaceholderSpriteFactory
{
    /// <summary>
    /// 创建水墨笔触矩形精灵：带不规则深色边框 + 纵向墨色渐变。
    /// </summary>
    /// <param name="width">像素宽</param>
    /// <param name="height">像素高</param>
    /// <param name="fillColor">填充色</param>
    /// <param name="strokeColor">边框色，默认浓墨</param>
    /// <param name="strokeWidth">边框宽度（像素）</param>
    public static Sprite CreateInkRect(int width, int height, Color fillColor,
        Color? strokeColor = null, int strokeWidth = 2)
    {
        Color stroke = strokeColor ?? ShuiMoPalette.InkDeep;
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float texAspect = (float)width / height;

        for (int y = 0; y < height; y++)
        {
            float yNorm = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;

                // 笔触边框：边缘像素 + 微小扰动模拟毛笔不规则边缘
                int distToEdge = Mathf.Min(x, width - 1 - x, y, height - 1 - y);
                float edgeNoise = Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 1.5f;
                float effectiveStroke = strokeWidth + edgeNoise;

                if (distToEdge < effectiveStroke)
                {
                    // 边框：从浓墨渐变到填充色
                    float t = distToEdge / effectiveStroke;
                    pixels[idx] = Color.Lerp(stroke, fillColor, t * t);
                }
                else
                {
                    // 填充区：纵向墨色渐变（底部更深，模拟墨汁沉淀）
                    float gradientFactor = 0.85f + yNorm * 0.15f;
                    // 添加纸纹噪点
                    float grain = (Mathf.PerlinNoise(x * 0.15f, y * 0.15f) - 0.5f) * 0.06f;
                    Color c = fillColor * gradientFactor;
                    c.r += grain;
                    c.g += grain;
                    c.b += grain;
                    pixels[idx] = c;
                }
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>
    /// 创建纯色矩形（无笔触效果，用于地面等大面积元素）。
    /// </summary>
    public static Sprite CreateRect(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                // 轻微纸纹
                float grain = (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.04f;
                pixels[idx] = new Color(
                    color.r + grain,
                    color.g + grain,
                    color.b + grain,
                    color.a
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>
    /// 创建墨迹圆形精灵：径向渐变，从中心浓墨到边缘淡出。
    /// 模拟毛笔在宣纸上点出的墨点效果。
    /// </summary>
    /// <param name="radius">像素半径</param>
    /// <param name="color">中心颜色</param>
    /// <param name="softness">边缘柔和度 0-1，0=硬边 1=最柔</param>
    public static Sprite CreateCircle(int radius, Color color, float softness = 0.4f)
    {
        int size = radius * 2;
        Texture2D tex = new Texture2D(size, size);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float distNorm = dist / radius;

                if (distNorm >= 1f)
                {
                    pixels[y * size + x] = Color.clear;
                    continue;
                }

                // 墨点样式：中心浓、边缘淡（非线性渐变）
                float alpha;
                if (distNorm < 0.3f)
                {
                    alpha = 1f; // 中心实心
                }
                else
                {
                    float t = (distNorm - 0.3f) / 0.7f;
                    alpha = 1f - Mathf.Pow(t, 1f + softness * 2f);
                }

                // 边缘噪声：让墨点边缘不规则
                float noise = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                alpha *= 0.8f + noise * 0.2f;

                Color c = color;
                c.a = Mathf.Clamp01(alpha);
                pixels[y * size + x] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // ====== 便捷方法 ======

    /// <summary>玩家精灵：靛蓝色笔触矩形 32x48</summary>
    public static Sprite PlayerSprite()
    {
        return CreateInkRect(32, 48, ShuiMoPalette.Indigo, ShuiMoPalette.InkBlack, 2);
    }

    /// <summary>敌人精灵（通用）：朱砂红色笔触矩形 32x48</summary>
    public static Sprite EnemySprite()
    {
        return CreateInkRect(32, 48, ShuiMoPalette.Vermillion, ShuiMoPalette.InkDeep, 2);
    }

    /// <summary>杂兵精灵：淡朱砂 28x40</summary>
    public static Sprite GruntSprite()
    {
        return CreateInkRect(28, 40,
            new Color(0.80f, 0.45f, 0.40f, 1f), ShuiMoPalette.InkDeep, 2);
    }

    /// <summary>弓手精灵：藤黄色 24x44</summary>
    public static Sprite ArcherSprite()
    {
        return CreateInkRect(24, 44, ShuiMoPalette.Gamboge, ShuiMoPalette.InkDeep, 2);
    }

    /// <summary>精英精灵：花青色 36x52</summary>
    public static Sprite EliteSprite()
    {
        return CreateInkRect(36, 52, ShuiMoPalette.FlowerBlue, ShuiMoPalette.InkBlack, 2);
    }

    /// <summary>Boss精灵：深朱砂 64x80</summary>
    public static Sprite BossSprite()
    {
        return CreateInkRect(64, 80,
            new Color(0.55f, 0.15f, 0.15f, 1f), ShuiMoPalette.InkBlack, 3);
    }

    /// <summary>地面精灵：宣纸色矩形</summary>
    public static Sprite GroundSprite()
    {
        return CreateRect(64, 16, ShuiMoPalette.AgedPaper);
    }

    /// <summary>墨迹粒子精灵：浓墨圆点 r=4</summary>
    public static Sprite InkParticleSprite()
    {
        return CreateCircle(4, ShuiMoPalette.InkBlack, 0.5f);
    }

    /// <summary>墨迹溅射精灵（大）：浓墨圆点 r=6</summary>
    public static Sprite InkSplashSprite()
    {
        return CreateCircle(6, ShuiMoPalette.InkBlack, 0.6f);
    }

    /// <summary>
    /// 水墨地面精灵：宣纸底色 + 横向毛笔皴擦纹理。
    /// 适合大面积地面/平台，有宣纸的质感和毛笔皴法的粗糙感。
    /// </summary>
    public static Sprite InkGroundSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float yNorm = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float xNorm = (float)x / width;

                // 宣纸底色
                Color baseColor = ShuiMoPalette.AgedPaper;

                // 横向皴擦纹理：模拟毛笔侧锋干笔擦过的痕迹
                float textureNoise = Mathf.PerlinNoise(x * 0.04f, y * 0.2f);
                float strokeNoise = Mathf.PerlinNoise(x * 0.02f, 0f);

                // 顶部边缘稍深（地面与空气交界）
                float edgeGradient = yNorm < 0.2f ? (0.2f - yNorm) / 0.2f : 0f;

                float darken = textureNoise * 0.06f + strokeNoise * 0.04f - edgeGradient * 0.15f;
                pixels[idx] = new Color(
                    baseColor.r - darken,
                    baseColor.g - darken,
                    baseColor.b - darken,
                    1f
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>
    /// 创建水墨面板精灵：宣纸色背景 + 四边墨迹装饰。
    /// 适合作为UI面板背景，有古朴的卷轴质感。
    /// </summary>
    public static Sprite CreateInkPanelSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float yNorm = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float xNorm = (float)x / width;

                // 宣纸底色
                Color baseColor = ShuiMoPalette.RicePaper;

                // 边缘装饰：四角加深墨迹
                float distToLeft = xNorm;
                float distToRight = 1f - xNorm;
                float distToTop = 1f - yNorm;
                float distToBottom = yNorm;

                float minDist = Mathf.Min(distToLeft, distToRight, distToTop, distToBottom);
                float edgeFactor = minDist < 0.08f ? (0.08f - minDist) / 0.08f : 0f;

                // 纸纹噪点
                float grain = (Mathf.PerlinNoise(x * 0.1f, y * 0.1f) - 0.5f) * 0.05f;

                // 四角墨迹装饰（模拟卷轴装裱）
                float cornerDarken = 0f;
                float cornerDist = Mathf.Min(
                    Vector2.Distance(new Vector2(xNorm, yNorm), Vector2.zero),
                    Vector2.Distance(new Vector2(xNorm, yNorm), Vector2.right),
                    Vector2.Distance(new Vector2(xNorm, yNorm), Vector2.up),
                    Vector2.Distance(new Vector2(xNorm, yNorm), Vector2.one)
                );
                if (cornerDist < 0.15f)
                {
                    cornerDarken = (0.15f - cornerDist) / 0.15f * 0.15f;
                }

                float darken = edgeFactor * 0.2f + cornerDarken - grain;
                pixels[idx] = new Color(
                    Mathf.Clamp01(baseColor.r - darken),
                    Mathf.Clamp01(baseColor.g - darken),
                    Mathf.Clamp01(baseColor.b - darken),
                    1f
                );
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// 创建粗糙矩形精灵：淡墨色，用于滑块背景。
    /// </summary>
    public static Sprite CreateRoughRectSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float grain = (Mathf.PerlinNoise(x * 0.2f, y * 0.2f) - 0.5f) * 0.08f;
                Color c = ShuiMoPalette.InkLight;
                c.r += grain;
                c.g += grain;
                c.b += grain;
                pixels[idx] = c;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// 创建水墨笔触条精灵：横向渐变笔触，用于滑动条填充。
    /// </summary>
    public static Sprite CreateInkStrokeSprite(int width, int height)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float yNorm = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float xNorm = (float)x / width;

                // 上下边缘淡出模拟笔触
                float distToEdge = Mathf.Min(yNorm, 1f - yNorm) / 0.5f;
                float edgeAlpha = distToEdge < 1f ? distToEdge : 1f;

                // 左右两端笔触加重
                float endFactor = 1f;
                if (xNorm < 0.1f)
                    endFactor = 0.7f + xNorm * 3f;
                else if (xNorm > 0.9f)
                    endFactor = 0.7f + (1f - xNorm) * 3f;

                // 纸纹噪点
                float grain = (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.1f;

                pixels[idx] = new Color(1f, 1f, 1f, Mathf.Clamp01(edgeAlpha * endFactor + grain));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    // ====== 自动武器特效精灵 ======

    /// <summary>
    /// 墨斩轨迹精灵：横向弧形笔触，中间浓两端淡。
    /// 模拟毛笔在宣纸上快速横扫的效果。
    /// </summary>
    public static Sprite CreateInkSlashSprite(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float halfH = height / 2f;

        for (int y = 0; y < height; y++)
        {
            float yNorm = (y - halfH) / halfH; // -1 to 1
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float xNorm = (float)x / width;

                // 横向渐变：两端淡出
                float endFade = 1f;
                if (xNorm < 0.15f) endFade = xNorm / 0.15f;
                else if (xNorm > 0.85f) endFade = (1f - xNorm) / 0.15f;

                // 纵向渐变：中间浓
                float vertFade = 1f - Mathf.Abs(yNorm) * 0.8f;

                // 笔触噪点
                float noise = (Mathf.PerlinNoise(x * 0.3f, y * 0.5f) - 0.5f) * 0.15f;

                // 弧形形状：上下边缘越靠近两端越收窄
                float arcNarrow = 1f;
                float distFromCenter = Mathf.Abs(xNorm - 0.5f) * 2f; // 0 at center, 1 at ends
                float edgeThreshold = 0.7f + distFromCenter * 0.3f;
                if (Mathf.Abs(yNorm) > edgeThreshold) arcNarrow = 0f;

                float alpha = endFade * vertFade * arcNarrow + noise;
                pixels[idx] = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// 墨柱精灵：纵向墨条，顶部浓底部淡出+墨滴效果。
    /// 模拟从天而降的墨汁柱。
    /// </summary>
    public static Sprite CreateInkColumnSprite(int width, int height, Color color)
    {
        Texture2D tex = new Texture2D(width, height);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[width * height];
        float halfW = width / 2f;

        for (int y = 0; y < height; y++)
        {
            float yNorm = (float)y / height; // 0=top, 1=bottom
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float xNorm = (x - halfW) / halfW; // -1 to 1

                // 顶部浓，底部淡出
                float vertFade = 1f - yNorm * yNorm;

                // 横向渐变：边缘淡
                float horizFade = 1f - Mathf.Abs(xNorm) * 0.7f;

                // 底部墨滴不规则延展
                float drip = 0f;
                if (yNorm > 0.8f)
                {
                    float dripT = (yNorm - 0.8f) / 0.2f;
                    float dripNoise = Mathf.PerlinNoise(x * 0.5f, 0f);
                    drip = dripT * (0.3f + dripNoise * 0.3f);
                }

                float noise = (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.1f;
                float alpha = vertFade * horizFade + drip + noise;

                pixels[idx] = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), 100f);
    }

    /// <summary>
    /// 墨弹精灵：墨滴飞弹，圆形带运动拖尾。
    /// </summary>
    public static Sprite CreateInkProjectileSprite(int radius, Color color)
    {
        int size = radius * 3; // 含尾部空间
        int tailLength = radius;
        Texture2D tex = new Texture2D(size + tailLength, size);
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[(size + tailLength) * size];
        Vector2 headCenter = new Vector2(radius, radius);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size + tailLength; x++)
            {
                int idx = y * (size + tailLength) + x;

                // 头部圆形
                float headDist = Vector2.Distance(new Vector2(x, y), headCenter);
                float headAlpha = 0f;
                if (headDist < radius)
                {
                    float t = headDist / radius;
                    headAlpha = t < 0.3f ? 1f : 1f - Mathf.Pow((t - 0.3f) / 0.7f, 2f);
                }

                // 尾部拖尾（在头部左侧）
                float tailAlpha = 0f;
                if (x < radius && headDist < radius * 1.2f)
                {
                    float tailT = (float)x / radius;
                    tailAlpha = tailT * 0.6f;
                }

                float noise = (Mathf.PerlinNoise(x * 0.4f, y * 0.4f) - 0.5f) * 0.15f;
                float alpha = Mathf.Max(headAlpha, tailAlpha) + noise;

                pixels[idx] = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size + tailLength, size), new Vector2(0.5f, 0.5f), 100f);
    }
}