# GameAPI 与 PolarisGameMenu API 归属说明

## 目的

明确 `PolarisAPI.Game` 与 `PolarisAPI.GameMenu` 的职责，消除游戏菜单相关 API 的重复入口。

## 确认结论

### 1. 查询游戏菜单：归 GameAPI

统一使用：

```csharp
PolarisAPI.Game.Menu.IsOpen
PolarisAPI.Game.Menu.Current
```

不再由 `PolarisAPI.GameMenu.IsOpen` 提供游戏页面状态查询。

### 2. 打开游戏菜单：归 GameAPI

统一使用：

```csharp
PolarisAPI.Game.Menu.Open()
PolarisAPI.Game.Menu.TryOpen(out GameMenu menu)
```

原 `PolarisAPI.GameMenu.Pause()` 迁移到 GameAPI。实现应统一走原版正常的菜单打开流程，不能保留两套
不同的打开逻辑。

### 3. 关闭游戏菜单：归 GameAPI

统一通过当前菜单实例关闭：

```csharp
PolarisAPI.Game.Menu.Current?.Close();
```

原 `PolarisAPI.GameMenu.Resume()` 迁移到 GameAPI。

### 4. 世界暂停策略：归 PolarisAPI.GameMenu

保留：

```csharp
PolarisAPI.GameMenu.SetWorldPause(bool enabled)
PolarisAPI.GameMenu.PauseWorldWhileOpen
```

移除重复且参数语义相反的：

```csharp
PolarisAPI.Game.World.SetPauseSimulation(bool simulation)
```

`SetWorldPause` 配置的是 Polaris 游戏菜单打开时是否暂停世界，因此属于菜单扩展策略，不属于游戏页面
实例操作。

### 5. 两种取消输入查询：都保留

```csharp
MainMenuAPI.IsCancelInputPressed()
PolarisAPI.Game.Input.WasPressed(GameInputAction.Cancel)
```

两者用途不同：

- `MainMenuAPI.IsCancelInputPressed()` 服务于标题菜单，按标题界面的固定 `Escape/X` 规则判断。
- `Game.Input.WasPressed(GameInputAction.Cancel)` 查询游戏动作映射，跟随玩家的改键设置。

它们不是重复 API，不进行合并。

## 最终归属

| 能力 | API |
| --- | --- |
| 查询游戏菜单 | `PolarisAPI.Game.Menu` |
| 打开游戏菜单 | `PolarisAPI.Game.Menu` |
| 关闭游戏菜单 | `GameMenu` 实例 |
| 设置菜单打开时是否暂停世界 | `PolarisAPI.GameMenu` |
| 注册游戏菜单分类 | `PolarisAPI.GameMenu` |
| 标题菜单固定取消键判断 | `MainMenuAPI` |
| 游戏动作取消键判断 | `PolarisAPI.Game.Input` |

## 迁移方式

如果需要兼容已有模组，先将旧入口标记为 `Obsolete` 并转发到新入口，在下一个允许破坏性变更的版本
删除：

| 旧 API | 新 API |
| --- | --- |
| `PolarisAPI.GameMenu.IsOpen` | `PolarisAPI.Game.Menu.IsOpen` |
| `PolarisAPI.GameMenu.Pause()` | `PolarisAPI.Game.Menu.Open/TryOpen` |
| `PolarisAPI.GameMenu.Resume()` | `PolarisAPI.Game.Menu.Current?.Close()` |
| `Game.World.SetPauseSimulation(bool)` | `PolarisAPI.GameMenu.SetWorldPause(bool)` |
