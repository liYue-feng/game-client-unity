# Input System 问题解决

## 问题
启动Unity时看到错误：
```
TypeInitializationException during event processing of Editor update; resetting event buffer
```

## 解决步骤

### 1. 重启Unity（最重要！）
第一次启用新InputSystem后必须重启Unity编辑器，这通常能解决问题。

### 2. 检查项目设置
- 打开：Edit → Project Settings → Player
- 找到：Other Settings → Configuration
- 确认：Active Input Handling 设为 "Input System Package" 或 "Both"
- 如果改了设置，Unity会提示重启

### 3. 生成C#类（可选）
本项目已预配置好InputActions，但如果你要自己生成：
1. 在Project窗口选中 `Assets/GameInput.inputactions`
2. Inspector中勾选 "Generate C# Class"
3. 点击 "Apply"，会生成 `GameInput.cs`

### 4. 按键映射
本项目已配置好：

| 操作 | 按键/手柄 |
|------|----------|
| 移动 | WASD / 方向键 / 左摇杆 |
| 攻击 | J / 鼠标左键 / 手柄A |
| 弹反 | L / 鼠标右键 / 手柄LT |
| 冲刺 | K / 空格 / 手柄B |
| 暂停 | ESC / 手柄Start |
| 背包 | Tab |

### 5. 向后兼容
代码同时支持：
- **NewInputHandler**：新InputSystem（优先）
- **InputHandler**：老InputManager（回退）

InputMediator会自动选择可用的那个。

## 资源
- `GameInput.inputactions`：输入配置文件
- `NewInputHandler.cs`：新系统的输入处理
- `InputMediator.cs`：统一输入接口

## 如果还是不行
1. 关闭Unity
2. 删除 `Library/ScriptAssemblies`
3. 重新打开Unity
