# AI生成的水墨资源目录

这个目录放你用AI生成的所有水墨画风游戏资源。

## 资源映射表

| 文件名 | 用途 | Unity路径 |
|--------|------|----------|
| Q版水墨黑衣小剑客.png | 玩家角色 | Assets/Resources/Sprites/Characters/Player.png |
| Q版水墨迷你僵尸.png | 杂兵敌人 | Assets/Resources/Sprites/Enemies/Grunt.png |
| Q版水墨墨色幽灵.png | Boss敌人 | Assets/Resources/Sprites/Enemies/Boss.png |
| Q版水墨武者对决.png | 标题画面/主菜单 | Assets/Resources/Sprites/Characters/TitleCharacter.png |

## 如何添加新资源

1. 把AI生成的图片放 AiRes/ 目录
2. 在 AiSpriteLoader.cs 中添加加载方法
3. 在对应脚本中调用新方法（如 BattleSceneSetup, WaveSpawner 等）
4. 复制到 Assets/Resources/Sprites/ 并创建 .meta 文件

## Unity资源加载规则

- 所有可动态加载的资源放 `Assets/Resources/` 下
- 用 `Resources.Load<Sprite>("路径不带后缀")` 加载
- 不要在 Resources 下放太多东西，会影响启动时间
