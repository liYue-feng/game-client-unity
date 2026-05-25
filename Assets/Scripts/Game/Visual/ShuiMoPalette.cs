using UnityEngine;

/// <summary>
/// 水墨调色板：shuimo-ui 设计系统的色卡。
/// 所有颜色以中国传统水墨画颜料命名。
///
/// 参考：shuimo-ui (janghood/shuimo-ui)
/// </summary>
public static class ShuiMoPalette
{
    // ====== 墨色系 (Ink Scale) ======
    /// <summary>焦墨 — 最黑，用于主要线条和文字</summary>
    public static readonly Color InkBlack      = new Color(0.10f, 0.10f, 0.10f, 1f);
    /// <summary>浓墨 — 用于深色渲染和阴影</summary>
    public static readonly Color InkDeep       = new Color(0.18f, 0.18f, 0.18f, 1f);
    /// <summary>重墨 — 用于中等深度的笔触</summary>
    public static readonly Color InkMedium     = new Color(0.30f, 0.30f, 0.30f, 1f);
    /// <summary>淡墨 — 浅色水墨渲染</summary>
    public static readonly Color InkLight      = new Color(0.55f, 0.55f, 0.55f, 1f);
    /// <summary>清墨 — 最淡的水墨痕迹</summary>
    public static readonly Color InkPale       = new Color(0.75f, 0.75f, 0.75f, 1f);

    // ====== 宣纸色 (Paper) ======
    /// <summary>宣纸白 — 带暖黄调的纸色，用作背景</summary>
    public static readonly Color RicePaper     = new Color(0.96f, 0.94f, 0.89f, 1f);
    /// <summary>旧宣纸 — 偏灰黄的旧纸色</summary>
    public static readonly Color AgedPaper     = new Color(0.90f, 0.87f, 0.80f, 1f);

    // ====== 传统颜料 (Traditional Pigments) ======
    /// <summary>朱砂红 — 醒目的红色，用于伤害和警告</summary>
    public static readonly Color Vermillion    = new Color(0.75f, 0.25f, 0.25f, 1f);
    /// <summary>花青 — 偏灰的蓝绿色，用于特殊效果</summary>
    public static readonly Color FlowerBlue    = new Color(0.23f, 0.42f, 0.42f, 1f);
    /// <summary>藤黄 — 暖黄色，用于警示和金币</summary>
    public static readonly Color Gamboge       = new Color(0.83f, 0.63f, 0.19f, 1f);
    /// <summary>赭石 — 暖棕色，用于地面和自然元素</summary>
    public static readonly Color BurntSienna   = new Color(0.55f, 0.35f, 0.17f, 1f);
    /// <summary>靛蓝 — 深蓝紫色，玩家主色</summary>
    public static readonly Color Indigo        = new Color(0.20f, 0.30f, 0.55f, 1f);
    public static readonly Color InkPurple     = new Color(0.35f, 0.15f, 0.45f, 1f);
    public static readonly Color JadeGreen     = new Color(0.15f, 0.55f, 0.35f, 1f);

    // ====== 别名（兼容旧代码） ======
    public static Color CinnabarRed => Vermillion;
    public static Color ThickInk => InkDeep;

    // ====== 工具方法 ======
    /// <summary>颜色线性插值</summary>
    public static Color Interpolate(Color a, Color b, float t)
    {
        return Color.Lerp(a, b, Mathf.Clamp01(t));
    }

    // ====== 水墨晕染 (Wash Gradients) ======
    /// <summary>浓墨到淡墨的渐变 — 用于笔触效果</summary>
    public static Color InkGradient(float t)
    {
        return Color.Lerp(InkBlack, InkPale, Mathf.Clamp01(t));
    }

    /// <summary>朱砂晕染 — 从饱和度高的红到淡红</summary>
    public static Color VermillionWash(float t)
    {
        return Color.Lerp(Vermillion, new Color(0.90f, 0.70f, 0.65f, 0.3f), Mathf.Clamp01(t));
    }
}