using UnityEngine;

/// <summary>
/// AI生成的水墨Sprite加载器。
/// 从 Resources/Sprites/ 加载你用AI生成的真实水墨画风图片。
///
/// 资源目录约定:
/// - Characters/Player.png → 玩家（Q版水墨黑衣小剑客）
/// - Enemies/Grunt.png → 杂兵（Q版水墨迷你僵尸）
/// - Enemies/Boss.png → Boss（Q版水墨墨色幽灵）
/// - Characters/TitleCharacter.png → 标题角色（Q版水墨武者对决）
///
/// 如果资源加载失败，自动回退到 PlaceholderSpriteFactory 生成的占位符。
/// </summary>
public static class AiSpriteLoader
{
    private static bool _resourcesLoaded = false;

    // 缓存的Sprite
    private static Sprite _playerSprite;
    private static Sprite _gruntSprite;
    private static Sprite _bossSprite;
    private static Sprite _titleCharacterSprite;

    /// <summary>
    /// 预加载所有AI生成的Sprite（建议在游戏启动时调用）。
    /// </summary>
    public static void PreloadAllSprites()
    {
        if (_resourcesLoaded) return;

        // 尝试从 Resources 加载
        _playerSprite = TryLoadSprite("Sprites/Characters/Player");
        _gruntSprite = TryLoadSprite("Sprites/Enemies/Grunt");
        _bossSprite = TryLoadSprite("Sprites/Enemies/Boss");
        _titleCharacterSprite = TryLoadSprite("Sprites/Characters/TitleCharacter");

        _resourcesLoaded = true;
        Debug.Log("[AiSpriteLoader] AI水墨资源加载完成");
    }

    /// <summary>
    /// 获取玩家Sprite（Q版水墨黑衣小剑客）。
    /// </summary>
    public static Sprite PlayerSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _playerSprite != null ? _playerSprite : PlaceholderSpriteFactory.PlayerSprite();
    }

    /// <summary>
    /// 获取杂兵Sprite（Q版水墨迷你僵尸）。
    /// </summary>
    public static Sprite GruntSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _gruntSprite != null ? _gruntSprite : PlaceholderSpriteFactory.GruntSprite();
    }

    /// <summary>
    /// 获取弓手Sprite（用杂兵占位，以后可以单独生成）。
    /// </summary>
    public static Sprite ArcherSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _gruntSprite != null ? _gruntSprite : PlaceholderSpriteFactory.ArcherSprite();
    }

    /// <summary>
    /// 获取精英Sprite（用Boss占位，以后可以单独生成）。
    /// </summary>
    public static Sprite EliteSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _bossSprite != null ? _bossSprite : PlaceholderSpriteFactory.EliteSprite();
    }

    /// <summary>
    /// 获取BossSprite（Q版水墨墨色幽灵）。
    /// </summary>
    public static Sprite BossSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _bossSprite != null ? _bossSprite : PlaceholderSpriteFactory.BossSprite();
    }

    /// <summary>
    /// 获取标题角色Sprite（Q版水墨武者对决）。
    /// </summary>
    public static Sprite TitleCharacterSprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _titleCharacterSprite != null ? _titleCharacterSprite : null;
    }

    /// <summary>
    /// 获取敌人通用Sprite（回退到占位符）。
    /// </summary>
    public static Sprite EnemySprite()
    {
        if (!_resourcesLoaded) PreloadAllSprites();
        return _gruntSprite != null ? _gruntSprite : PlaceholderSpriteFactory.EnemySprite();
    }

    // ====== 继续提供占位符的便捷方法 ======

    public static Sprite GroundSprite() => PlaceholderSpriteFactory.GroundSprite();
    public static Sprite InkParticleSprite() => PlaceholderSpriteFactory.InkParticleSprite();
    public static Sprite InkSplashSprite() => PlaceholderSpriteFactory.InkSplashSprite();
    public static Sprite InkGroundSprite(int width, int height) => PlaceholderSpriteFactory.InkGroundSprite(width, height);
    public static Sprite CreateInkPanelSprite(int width, int height) => PlaceholderSpriteFactory.CreateInkPanelSprite(width, height);
    public static Sprite CreateRoughRectSprite(int width, int height) => PlaceholderSpriteFactory.CreateRoughRectSprite(width, height);
    public static Sprite CreateInkStrokeSprite(int width, int height) => PlaceholderSpriteFactory.CreateInkStrokeSprite(width, height);

    // ====== 内部帮助方法 ======

    private static Sprite TryLoadSprite(string path)
    {
        var tex = Resources.Load<Texture2D>(path);
        if (tex == null)
        {
            Debug.LogWarning($"[AiSpriteLoader] 资源加载失败: {path}，回退到占位符");
            return null;
        }

        // 把Texture2D转成Sprite，pivot在底部中心
        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0f), 100f);
    }
}
