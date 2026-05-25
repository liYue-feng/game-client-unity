#!/bin/bash
# ============================================================
# 小森平免费音效下载助手
# ============================================================
# 用法: bash download_sounds.sh [目标目录]
# 默认下载到: Assets/Resources/Sounds/
#
# 小森平音效库: https://taira-komori.net/freesounden.html
# 免费可商用，2000+音效，WAV格式
#
# 注意:
# - 此脚本生成下载指引，告诉你每个音效从哪个页面下载
# - 由于网站需要手动浏览选择，这里列出每个音效的推荐下载页
# - 下载后将 .wav 文件放入 Assets/Resources/Sounds/ 即可
# ============================================================

OUTDIR="${1:-Assets/Resources/Sounds}"

echo "============================================"
echo "  小森平音效下载指引"
echo "  主页: https://taira-komori.net/freesounden.html"
echo "============================================"
echo ""
echo "所有音效免费可商用，下载后放入: $OUTDIR"
echo ""

cat << 'EOF'
## 战斗音效 — SAMURAI / NINJA 分类
  页面: https://taira-komori.net/jidaigeki01en.html
  需要的音效:
    sword_hit.wav      → Sword → 刀剑斩击肉体声，短促有力
    katana_whoosh.wav  → Katana → 刀剑挥舞破风声
    sword_clash.wav    → Sword → 刀剑碰撞金属声，清脆短促
    heavy_slash.wav    → Cutting → 重斩击打声，低沉

## 打击音效 — FIGHTING 分类
  页面: https://taira-komori.net/attack01en.html
  需要的音效:
    punch_heavy.wav    → Punching → 重拳打击肉体声
    hurt_grunt.wav     → Damage → 受击闷哼
    body_fall.wav      → Damage → 沉重倒地声

## 爆炸/大型音效 — ARMS / BOMB 分类
  页面: https://taira-komori.net/arms01en.html
  需要的音效:
    big_crash.wav      → Explosion → 大型碎裂/冲击

## 移动音效 — HUMAN / FOOTSTEPS 分类
  页面: https://taira-komori.net/human01en.html
  需要的音效:
    dash_whoosh.wav    → Running → 快速冲刺掠过声
    footstep_sand.wav  → Walking → 沙石地面脚步声

## UI音效 — GAME BUTTON 分类
  页面: https://taira-komori.net/game01en.html
  需要的音效:
    ui_tap.wav         → Button → 轻柔点击
    ui_confirm.wav     → Correct → 确认音效，清脆上扬
    ui_cancel.wav      → Wrong → 取消/错误
    coin_pickup.wav    → Coin → 金币拾取，清脆叮当
    sparkle_collect.wav→ Coin → 轻闪光收集
    power_up.wav       → PowerUp-Down → 升级强化
    victory_fanfare.wav→ PowerUp-Down → 胜利号角

## 魔法/技能 — MAGIC / FANTASY 分类
  页面: https://taira-komori.net/magic01en.html
  需要的音效:
    magic_charge.wav   → Magic → 能量蓄力
    buff_aura.wav      → Fantasy → 光环加身
    game_over.wav      → Fantasy → 低沉哀伤

## 环境音 — NATURE 分类
  页面: https://taira-komori.net/nature01en.html
  需要的音效:
    gentle_wind.wav    → Wind → 竹林微风
    light_rain.wav     → Rain → 细雨声

============================================

EOF

echo "目标目录: $OUTDIR"
echo ""
echo "下载完成后，在Unity中运行 'Tools > Reload Sound Catalog' 即可加载。"
echo ""
echo "建议也试试 MiniMax Music 2.6 生成 BGM:"
echo "  主菜单: 古琴独奏，悠远空灵，水墨画意境"
echo "  战斗:   密集鼓点+琵琶扫弦，紧张急促"
echo "  Boss:   大鼓重击+唢呐高亢，黑暗压迫"
echo "============================================"