using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 音效映射表：将游戏内 clipName 映射到小森平免费音效库的具体文件。
///
/// 小森平音效库地址: https://taira-komori.net/freesounden.html
/// 免费可商用，2000+音效。
///
/// 使用方法:
/// 1. 访问上述网站，在对应分类中找到推荐音效
/// 2. 下载 .wav 文件放到 Assets/Resources/Sounds/ 目录
/// 3. AudioManager 启动时自动加载
/// </summary>
public static class SoundCatalog
{
    /// <summary>
    /// 音效映射: clipName → (小森平分类页面, 推荐音效描述, 建议本地文件名)
    /// </summary>
    public struct SoundEntry
    {
        public string categoryPage;   // 小森平分类页面 URL path
        public string description;    // 推荐音效描述（帮助在页面中定位）
        public string suggestedFile;  // 建议保存的本地文件名
    }

    public static readonly Dictionary<string, SoundEntry> Catalog = new Dictionary<string, SoundEntry>
    {
        // ===== 战斗音效 (SAMURAI / NINJA 分类) =====
        // 页面: https://taira-komori.net/jidaigeki01en.html
        ["hit"] = new SoundEntry
        {
            categoryPage = "jidaigeki01en.html",
            description = "Sword → 'Cutting flesh' or 'Slash hit' — 刀剑斩击肉体声，短促有力",
            suggestedFile = "sword_hit.wav"
        },
        ["slash"] = new SoundEntry
        {
            categoryPage = "jidaigeki01en.html",
            description = "Katana → 'Swing whoosh' — 刀剑挥舞破风声，0.3秒左右",
            suggestedFile = "katana_whoosh.wav"
        },
        ["parry"] = new SoundEntry
        {
            categoryPage = "jidaigeki01en.html",
            description = "Sword → 'Clash metal' — 刀剑碰撞金属声，清脆短促",
            suggestedFile = "sword_clash.wav"
        },
        ["heavy_hit"] = new SoundEntry
        {
            categoryPage = "jidaigeki01en.html",
            description = "Cutting → 'Heavy slash impact' — 重斩击打声，低沉有力",
            suggestedFile = "heavy_slash.wav"
        },

        // ===== 打击音效 (FIGHTING 分类) =====
        // 页面: https://taira-komori.net/attack01en.html
        ["punch"] = new SoundEntry
        {
            categoryPage = "attack01en.html",
            description = "Punching → 'Heavy punch impact' — 重拳打击肉体声",
            suggestedFile = "punch_heavy.wav"
        },
        ["damage_taken"] = new SoundEntry
        {
            categoryPage = "attack01en.html",
            description = "Damage → 'Grunt hurt' — 受击闷哼/痛呼",
            suggestedFile = "hurt_grunt.wav"
        },

        // ===== 死亡/击杀 (FIGHTING + ARMS 分类) =====
        ["death"] = new SoundEntry
        {
            categoryPage = "attack01en.html",
            description = "Damage → 'Body fall heavy' — 沉重倒地声",
            suggestedFile = "body_fall.wav"
        },
        ["boss_death"] = new SoundEntry
        {
            categoryPage = "arms01en.html",
            description = "Explosion → 'Big crash / Impact' — 大型爆炸/碎裂，震撼",
            suggestedFile = "big_crash.wav"
        },

        // ===== 移动音效 (HUMAN / FOOTSTEPS 分类) =====
        // 页面: https://taira-komori.net/human01en.html
        ["dash"] = new SoundEntry
        {
            categoryPage = "human01en.html",
            description = "Running → 'Swift dash whoosh' — 快速冲刺掠过声",
            suggestedFile = "dash_whoosh.wav"
        },
        ["footstep"] = new SoundEntry
        {
            categoryPage = "human01en.html",
            description = "Walking → 'Sand/gravel step' — 沙石地面脚步声",
            suggestedFile = "footstep_sand.wav"
        },

        // ===== UI 音效 (GAME BUTTON 分类) =====
        // 页面: https://taira-komori.net/game01en.html
        ["ui_click"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "Button → 'Soft tap / Click' — 轻柔点击，类似毛笔触纸",
            suggestedFile = "ui_tap.wav"
        },
        ["ui_confirm"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "Correct → 'Positive chime' — 确认音效，清脆上扬",
            suggestedFile = "ui_confirm.wav"
        },
        ["ui_cancel"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "Wrong → 'Negative buzz' — 取消/错误，低沉短音",
            suggestedFile = "ui_cancel.wav"
        },
        ["ui_coin"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "Coin → 'Coin pickup chime' — 金币拾取，清脆叮当",
            suggestedFile = "coin_pickup.wav"
        },

        // ===== 升级/奖励 (GAME BUTTON + MAGIC 分类) =====
        ["levelup"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "PowerUp-Down → 'Power up fanfare' — 升级/强化，上扬旋律",
            suggestedFile = "power_up.wav"
        },
        ["exp_pickup"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "Coin → 'Light sparkle collect' — 轻闪光收集，细小清脆",
            suggestedFile = "sparkle_collect.wav"
        },

        // ===== 技能/魔法 (MAGIC / FANTASY 分类) =====
        // 页面: https://taira-komori.net/magic01en.html
        ["special_skill"] = new SoundEntry
        {
            categoryPage = "magic01en.html",
            description = "Magic → 'Energy charge + release' — 能量蓄力+释放，有水墨爆发感",
            suggestedFile = "magic_charge.wav"
        },
        ["buff_activate"] = new SoundEntry
        {
            categoryPage = "magic01en.html",
            description = "Fantasy → 'Light aura / Blessing' — 光环加身，空灵上扬",
            suggestedFile = "buff_aura.wav"
        },

        // ===== 环境音 (NATURE 分类) =====
        // 页面: https://taira-komori.net/nature01en.html
        ["ambient_wind"] = new SoundEntry
        {
            categoryPage = "nature01en.html",
            description = "Wind → 'Gentle breeze' — 竹林微风，宁静氛围",
            suggestedFile = "gentle_wind.wav"
        },
        ["ambient_rain"] = new SoundEntry
        {
            categoryPage = "nature01en.html",
            description = "Rain → 'Light rain' — 细雨声，水墨意境",
            suggestedFile = "light_rain.wav"
        },

        // ===== BGM 替代（用 MiniMax 生成，这里放占位） =====
        ["bgm_menu"] = new SoundEntry
        {
            categoryPage = "instrument01en.html",
            description = "Instrument → 'Shakuhachi solo' — 尺八独奏，空灵悠远 (建议用MiniMax生成国风BGM替换)",
            suggestedFile = "bgm_menu.wav"
        },
        ["bgm_battle"] = new SoundEntry
        {
            categoryPage = "instrument01en.html",
            description = "Instrument → 'Taiko drum rhythm' — 太鼓节奏，紧张急促 (建议用MiniMax生成国风BGM替换)",
            suggestedFile = "bgm_battle.wav"
        },
        ["bgm_boss"] = new SoundEntry
        {
            categoryPage = "instrument01en.html",
            description = "Instrument → 'Big drum heavy' — 大鼓重击，压迫感 (建议用MiniMax生成国风BGM替换)",
            suggestedFile = "bgm_boss.wav"
        },

        // ===== 游戏结束 =====
        ["game_over"] = new SoundEntry
        {
            categoryPage = "magic01en.html",
            description = "Fantasy → 'Sad/dark tone' — 低沉哀伤，水墨消散",
            suggestedFile = "game_over.wav"
        },
        ["victory"] = new SoundEntry
        {
            categoryPage = "game01en.html",
            description = "PowerUp-Down → 'Grand fanfare' — 胜利号角，古筝扫弦感",
            suggestedFile = "victory_fanfare.wav"
        },
    };

    /// <summary>获取某个音效的推荐下载页面完整URL</summary>
    public static string GetDownloadUrl(string clipName)
    {
        if (Catalog.TryGetValue(clipName, out var entry))
        {
            return $"https://taira-komori.net/{entry.categoryPage}";
        }
        return "https://taira-komori.net/freesounden.html";
    }
}