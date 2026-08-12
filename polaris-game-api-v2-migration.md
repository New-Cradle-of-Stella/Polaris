# Polaris 游戏 API v2 迁移记录

> 状态：**已执行**
> 执行日期：2026-08-12
> 迁移前基线：`b614e8b7881364efe6e7e5d3fc4233aa59951f64`
> 规范源：`Polaris-Game-API-Spec-v2-静态与实例模型.xlsx`（工作表 `API规范_v2`，206 条契约）
> 验证脚本：`tools/check-api-spec.py`

本文替代原先的 `polaris-game-api-v2-pruning-plan.md`（计划稿）。计划里的分阶段方案已经执行完毕，
这里记录**最终落地的结果**、与计划的偏差，以及仍然待办的部分。

## 1. 结论

`Polaris.dll` 的游戏 API 已经从"长生命周期服务对象 + Handle/Snapshot/Result"模型
完整迁移到规范规定的"静态入口 + 活实例"模型。

自动校验结果（`python tools/check-api-spec.py --config Release`）：

```
spec contracts : 206
matched        : 206
missing        : 0
unexpected     : 0
```

- Debug / Release 均构建通过，**0 警告 0 错误**（迁移前存在的 3 条警告一并清掉，见 §6）。
- 206 条契约全部存在于程序集公共面。
- 受检类型（13 个静态分组 + `Audio.Bgm` + `Callbacks` + 10 个实例类型）上**没有**规范之外的公开成员。
- 旧游戏 API 的公开类型已全部从程序集中消失（反射核对，见 §5）。

## 2. 范围决策（对应计划中的 G0–G3）

| 闸门 | 决定 |
| --- | --- |
| G0 清理范围 | **只收敛游戏 API**。`MainMenu`、`GameMenu`（分类扩展）、`Settings`、`Localization`、`Modules`、`Paths`、`Types`、`Errors`、`Health`、`PolarisResAPI`、`PolarisUIAPI` 等产品 API 保留——表格标题是"游戏 API 规范"，没有描述这些能力，把它们一起删掉是产品决策而不是重构 |
| G1 表格第 42 行 | `SetWeather` 的签名格曾误写成 `HasWeather`。实现为 `static bool SetWeather(GameWeather weather)`，**表格已改正** |
| G2 表格第 44 行 | 标签曾写作 `DangerMeter` 而签名是 `GetDangerMeter`。实现为 `GetDangerMeter`，**表格已改正** |
| G3 签名依赖类型 | 允许公开。它们构成"签名依赖闭包"，在校验脚本的 `SIGNATURE_CLOSURE` 里显式列出 |

G1 / G2 原本是表格自身的笔误。两处都已在表格里改正，因此校验脚本<b>不再保留任何标签豁免</b>：
表格标签与程序集成员名一一对应，对不上就是真的对不上。

## 3. 最终形态

### 3.1 静态入口

`PolarisAPI.Game` 从"返回 `GameStateAPI` 实例的属性"变成**嵌套静态类**（`Api/Game/PolarisGameAPI.cs`）：

```
PolarisAPI.Game
├── Loop        FrameCount / HasFocus
├── Input       MousePosition / MouseWheelDelta / IsHeld / WasPressed / WasReleased / GetDirection / ClearState
├── Assets      LoadStage
├── Localization CurrentLocale / DefaultLocale / Change / IsCurrent
├── World       CurrentMap / CurrentPlayer / ChangeMap / IsNight / HasWeather / SetWeather /
│               DangerLevel / GetDangerMeter / DangerBonus / ClearWeather / ShuffleWeather /
│               SetPauseSimulation / BattleCount
├── Items       Resolve
├── Inventory   Main / Precious / Enhancer / House
├── Menu        Current / Open
├── Events      Current / Start / Change
├── Quests      Get / Head
├── Economy     MaxAmount / GetAmount / Add / Spend
├── Audio       IsReady / SfxVolume / VoiceVolume / BgmVolume / MasterVolume / Play
│   └── Bgm     Load / Play / Stop / FadeIn / FadeOut / Replace / IsPlaying / CurrentTrack
└── Callbacks   Register<TData>
```

### 3.2 实例模型

十个包装类型继承自 `GameInstance`（`Api/Game/GameInstance.cs`），共享三条规则：

1. **身份稳定** —— 同一个游戏对象在存活期内永远给出同一个包装器实例，可直接用作字典键。
   实现见 `Api/Game/Internal/InstanceTable.cs`（按**引用相等**做键；`UnityEngine.Object`
   重写过 `Equals` 把"已销毁"当成 `null` 比，直接当字典键会让哈希与相等对不上）。
2. **失效即拒绝** —— 只读成员返回零值/空值，写操作抛 `InvalidGameInstanceException`。
   安静地作用到对象池里的"下一任住客"身上是这类 API 最难查的一类 bug。
3. **回调随实例失效** —— 实例作废时，挂在它上面的注册一并停止，调用方不需要善后。

| 实例类型 | 底层绑定 | 失效判据 |
| --- | --- | --- |
| `GameMap` | `m2d.Map2d` | `Map2d.closed` |
| `GameCharacter` | `m2d.M2Attackable` | Unity 销毁判定 + 地图代数 |
| `GamePlayer` | `nel.PR` | 同上 |
| `GameEnemy` | `nel.NelEnemy` | 同上 |
| `GameItem` | `nel.NelItem` | 永不失效（静态游戏数据） |
| `GameStorage` | `nel.ItemStorage` | 引用是否还在 |
| `GameAudioPlayback` | `XX.SndPlayer` | 播放结束并被回收 |
| `GameMenu` | `nel.gm.UiGameMenu` | `state == OFFLINE` |
| `GameEvent` | 事件 key | `EV.isActive(key, front)` |
| `GameQuest` | 任务 key | 任务追踪器是否可用 |

`GameCharacter.Wrap` 会优先给出更具体的 `GamePlayer` / `GameEnemy`——否则每个调用方都要再 `as` 一次。

### 3.3 回调

单一的 151 值 `GameCallbackKind` 拆成 **28 个 `GameStaticCallbackKind` + 26 个 `GameInstanceCallbackKind`**，
旧枚举里其余的值退出公共契约。

- 注册：静态走 `PolarisAPI.Game.Callbacks.Register<TData>`，实例走实例自己的 `Register<TData>`。
- **注册时类型校验**：`GameCallbackContract` 是"种类 ↔ 负荷类型"的唯一真相表，
  `TData` 不匹配、或者这种回调不属于当前实例类型（例如把敌人回调注册到存储上）时立刻抛
  `ArgumentException`。放到派发时才检查是不行的——那时调用栈里已经没有注册点了，
  而事件可能要玩很久才触发一次。
- 派发：`GameCallbackHub` 按 `(种类, 实例编号)` 分组，copy-on-write 数组快照；
  真正调用订阅者仍然走 `Infra.CallbackRuntime.Drain()`，因此所有回调在同一条时间线上按发生顺序执行。
- `GameCallbackOptions` 的 `Priority` / `Once` / `DebugName` 语义保持不变，`GameSubscription`
  更名为 `GameCallbackRegistration`。

**发布源分两类**，选择依据是"这件事读一个字段能不能知道"：

| 方式 | 覆盖的回调 | 理由 |
| --- | --- | --- |
| 每帧状态差分（`GameRuntime`） | 地图切换/打开/关闭、日夜、夜等级、危险度、天气、语言、金钱、音量、BGM 曲目与播放状态、重点任务、玩家/敌人状态与生死、输入按下/释放 | 这些量在游戏里有多条写入路径（事件脚本、UI、存档读入、内部推进），逐条打补丁要跟着游戏版本追一整串调用链，而读一个字段就是最终结果 |
| Harmony 补丁（`Patch/Callbacks/**`） | 存读档、自动保存、物品增减/转移/获得/使用、掉落生成、剧情旗标、任务更新与移除、菜单开关、事件开关、伤害与恢复、状态效果、击退 | 这些"发生过"在状态上看不出来，只能在调用点截获 |

补丁按领域合并成 5 个文件（原先 31 个），Harmony 目标与"怎么认出这件事发生了"的判断逻辑
全部沿用迁移前已验证的写法。

## 4. 与计划的偏差

| 计划 | 实际 | 原因 |
| --- | --- | --- |
| `GameCharacter.Position` 之类便利成员 | 未公开 | 规范没有列这一项，而"允许清单"必须是严格的，否则清理就变成"加了新的、旧的也还在" |
| `PR.changeState` / `initDeath` 等状态补丁 | 删除，改为每帧差分 | 死亡与复活在游戏里有多条入口（伤害致死、事件强制、游戏结束恢复、替身猫复活），逐条打补丁不划算；状态字段差分能覆盖全部路径 |
| `GameEnemy.ApplyDamage` 走 `NelEnemy.applyDamage(NelAttackInfo, …)` | 走继承来的 `applyHpDamage`/`applyMpDamage` | 构造一个合法的 `NelAttackInfo` 需要一整套未核实的内部约定；继承路径已经过验证，且直接返回实际扣血量 |
| `GameMap.Time` 读游戏字段 | 由包装器按游戏帧计数（`XX.IN.totalframe`，60 帧 = 1 秒）自行累计 | `Map2d` 上没有"累计运行时间"这样的成员。用游戏自己的帧计数意味着读档、演出与暂停期间不推进，这正是该有的语义 |
| 非游戏 API 的 `GameActionResult` | 改名为 `PolarisActionResult` / `PolarisActionStatus` | `GameActionResult` 属于被删除的 v1 游戏 API；`GameMenuAPI` 等产品 API 需要自己的结果类型 |
| Unity 场景生命周期、`FixedUpdating`、`FocusChanged`、`Stopping`、`ApplicationPauseChanged` 回调 | 删除 | 不在规范的 54 条回调里 |

`GameStateAPI` 上不属于规范的三项能力（`IsMtrxReady` / `WhenReady` / `LocaleChanged`）
没有丢，而是内部化到 `API.GameSessionRuntime`：它们服务的是 Polaris 自己的资源、本地化与 PUI
子系统，不是下游内容模组的游戏 API。

## 5. 验证

### 5.1 已完成

- `dotnet build Polaris.csproj -c Debug` / `-c Release`：0 错误，0 警告。
- `python tools/check-api-spec.py --config Release`：206/206，missing 0，unexpected 0。
- 反射核对旧公开类型已全部消失：`GameStateAPI`、`GameLoopAPI`、`InputGameAPI`、`WorldGameAPI`、
  `CharacterGameAPI`、`PlayerGameAPI`、`InventoryGameAPI`、`EconomyGameAPI`、`CombatGameAPI`、
  `AudioGameAPI`、`CharacterHandle`、`ItemIdentity`、`AudioHandle`、各类 `Snapshot`、
  `GameActionResult`、`GameCapabilities`、`GameCallbackKind`、`GameSignal<T>`、`GameFastSignal`、
  `GameSubscription`、`GameCallbackStatus`、`GameCallbackDescriptor`、`GameCallbacksAPI`。
- 仓库内部对旧入口的全部引用已迁移（`Plugin`、`PUI`、`Res`、`Lang`、`Localization`、`Diagnostics`）。

### 5.2 待办：游戏内验证

编译期与公共面已经是绿的，**运行时行为尚未在游戏里跑过**。下列项目需要实机冒烟：

- 启动、新游戏、读档、存档、自动存档，以及读档后旧实例是否确实全部作废。
- 切图前后：`GameMap` 的开/关回调、上一张图的角色包装器是否整体失效。
- `World.ChangeMap` / `SetWeather` / `Menu.Open` / `Events.Start` / `Events.Change` 这几条
  **新增的写入路径**——迁移前它们都是 `Unsupported`，是本次改动里游戏侧风险最高的部分。
- `GameStorage.Use` / `Drop`：`NelItem.Use(pr, storage, grade, null)` 的第四个参数
  （`IItemUser`）传 `null` 是否被游戏接受。
- `GameEnemy.AddKnockback`：`addKnockbackVelocity(v, null, null, default)` 的两个 `null`
  是否被游戏接受。
- 回调矩阵：54 条各触发一次，核对负荷类型、优先级顺序、`Once` 只执行一次、`Dispose` 后不再触发，
  以及一个实例的回调不会收到另一个实例的事件。

上面三项带 `null` 的原生调用都包在 try/catch 里并经 `PolarisAPI.Errors` 上报，
最坏情况是这一次调用无效而不是崩游戏；但"无效"本身需要实机确认。

## 6. 构建警告清零

迁移前仓库带着 3 条常驻警告。它们与 v2 无关，但既然要把公共面收干净，构建输出也该是干净的。
两条都从源头解决，没有用 `NoWarn` 盖掉——盖掉之后下一个真问题也会跟着一起看不见。

| 警告 | 根因 | 处理 |
| --- | --- | --- |
| `CS0436` ×2 | Krafs.Publicizer 以 contentfile 形式塞进来一份 `IgnoresAccessChecksToAttribute` 源码，而 BepInEx 6 依赖的 MonoMod.Utils 里已经公开了同名类型 | 在 `Polaris.csproj` 加 `DropDuplicateIgnoresAccessChecksTo` 目标，把那份重复源码移出编译；`[assembly: IgnoresAccessChecksTo]` 改为绑定 MonoMod.Utils 的类型。核对产物：两条 `IgnoresAccessChecksTo("Assembly-CSharp")` / `("unsafeAssem")` 仍在，Publicizer 的访问检查策略未改动 |
| `BepInEx002` | `BepInEx.Analyzers` 1.x 是 BepInEx 5 时代的包，把基类名硬编码成 `BepInEx.BaseUnityPlugin`；本项目用的是 BepInEx 6 的 `BepInEx.Unity.Mono.BaseUnityPlugin`，于是这条规则对 `Plugin` 恒为假阳性 | 移除该 PackageReference。它只有两条规则（BepInEx001/002），都按 v5 类型名匹配，在这个项目里都不可能命中真问题 |

刻意<b>没有</b>改 `PublicizerRuntimeStrategies`：那会换掉整个访问检查策略，属于运行时行为改动，
不是消一条警告该付的代价。

## 7. 版本

`Polaris.csproj` 的 `Version` 提升到 **2.0.0**。这是破坏性版本：不保留任何 `[Obsolete]` 兼容别名，
下游模组必须按 §3 的新模型改写。README 的中英文两侧都已加入 v2 游戏 API 章节与示例。
