# GameAPI v2 → v3 分阶段实现计划（Claude 执行版）

## 1. 使用方式

本文用于让 Claude 把当前代码中的 GameAPI v2 分阶段升级到 v3，同时落实已经确认的 API 归属，消除菜单与暂停策略冲突，并加入游戏内插件（Enhancer）和技能（Skill）支持。

一次只执行一个编号阶段。阶段没有通过构建、测试和退出条件时，不得继续下一阶段；不得顺手预埋后续阶段的大段代码。

需求真相按以下优先级读取：

1. `GameAPI-游戏页面与菜单边界说明.md`：菜单、暂停和取消输入冲突的最终裁决。
2. `Polaris-Game-API-Spec-v3-静态与实例模型.xlsx`：v3 公共 API 名称、签名和说明。
3. 本文：实施顺序、生命周期、回调顺序、验证要求和并行边界。
4. 当前 v2 代码：已有 API 的行为基线，不能把现有实现反向当成 v3 规范。
5. `C:\Users\Administrator\Documents\polarisDocs` 下的技术文档。
6. 当前游戏 `Assembly-CSharp.dll` 的反编译结果和必要的运行验证。

规范发生冲突时不得静默选择。已知例外是：v3 表格仍列有 `PolarisAPI.Game.World.SetPauseSimulation(bool)`，但边界说明已经明确要求移除，故以边界说明为准。

## 2. 当前差异基线

当前 v2 表格有 206 条 API/回调记录，修改后的 v3 有 245 条，主要新增 39 条：

- `PolarisAPI.Game.Enhancers`：5 个静态入口。
- `PolarisAPI.Game.Skills`：2 个静态入口。
- `GameEnhancer`：11 个实例成员。
- `GameSkill`：16 个实例成员。
- Enhancer/Skill：5 个实例回调。

另外有两处表格校正：

- `World.DangerMeter` 的名称改为与实际签名一致的 `GetDangerMeter`；当前代码已经是正确名称。
- `World.SetWeather` 的错误签名改为 `bool SetWeather(GameWeather weather)`；当前代码已经正确。

当前代码仍有以下 v2 冲突：

- `PolarisAPI.GameMenu.IsOpen/Pause/Resume` 与 `PolarisAPI.Game.Menu` 重叠。
- `PolarisAPI.Game.World.SetPauseSimulation(bool)` 与 `PolarisAPI.GameMenu.SetWorldPause(bool)` 重叠且布尔语义相反。
- `Game.Menu.Open()` 直接调用 `UiGameMenu.activate()`，而旧 `GameMenuAPI.Pause()` 使用原版 `menu_open` 请求流程，存在两套打开逻辑。
- v3 表格已有 `Menu.Current/Open`，但还应按边界说明补齐 `Menu.IsOpen/TryOpen`。
- `MainMenuAPI.IsCancelInputPressed()` 与 `Game.Input.WasPressed(Cancel)` 用途不同，必须同时保留。

物品 API 已足够作为 Enhancer/Skill 的关联物品视图，不新增第二套物品定义 API：

- Enhancer 通过 `GameEnhancer.Item` 返回 `GameItem`。
- Skill 通过 `GameSkill.BookItem` 返回 `GameItem`。
- 数量仍由 `GameStorage` 负责，不能在 `GameEnhancer` 或 `GameSkill` 中复制库存接口。

## 3. 每阶段硬限制

- 每阶段建议 350–900 行有效变更；自然工作量不足时以完整测试补齐，不得制造无用抽象或重复注释。
- 预计超过 900 行时，只能按可独立构建、可独立验收的内聚边界拆成 A/B 两阶段。
- 每阶段只交付一种主要能力。
- 不修改与当前阶段无关的 PEVT、PolarisRes、PUI 或用户未提交文件。
- 开始前和结束后都运行 `git status --short`；禁止 `git reset --hard`、还原或格式化无关文件。
- 使用 `git diff --numstat` 核对变更规模，并人工排除生成文件、锁文件和无意义行。
- 公共 API 不得暴露 `nel.*`、`m2d.*`、`UnityEngine.*`、`XX.*` 或游戏私有枚举。
- 所有游戏状态读写只能在主线程执行；不得在后台线程读取原版静态目录或库存。
- 查询失败返回 v3 约定的空值/状态；写操作必须明确成功、无变化、状态拒绝或底层失败，不能吞掉部分写入。
- 回调只在状态实际变化后触发；由 Polaris 发起的修改和原版 UI/事件发起的修改都只能各触发一次。
- `EnhancerActiveChanged` 与 `SkillEnabledChanged` 必须在原版属性/技能连接重算完成后入队。
- 不缓存本地化标题和说明；每次读取都跟随当前语言。

## 4. 目标代码布局

共享文件：

- `Api/Game/PolarisGameAPI.cs`：现有 v2 入口；只负责保留的共享 API 与 `Game` partial 接缝。
- `Api/Game/GameEnums.cs`：公共 Skill/Enhancer 枚举。
- `Api/Game/GameInstance.cs`：实例模型说明和公共生命周期。
- `Api/Game/Callbacks/GameCallbackKinds.cs`
- `Api/Game/Callbacks/GameCallbackData.cs`
- `Api/Game/Callbacks/GameCallbackContract.cs`
- `Api/Game/Internal/GameRuntime.cs`：最终统一泵与生命周期接线。

Enhancer 独占文件：

- `Api/Game/PolarisGameEnhancersAPI.cs`
- `Api/Game/GameEnhancer.cs`
- `Api/Game/Internal/GameEnhancerRuntime.cs`
- `Polaris.GameApi.Tests/GameEnhancer*Tests.cs`

Skill 独占文件：

- `Api/Game/PolarisGameSkillsAPI.cs`
- `Api/Game/GameSkill.cs`
- `Api/Game/Internal/GameSkillRuntime.cs`
- `Polaris.GameApi.Tests/GameSkill*Tests.cs`

菜单冲突文件：

- `Api/Game/PolarisGameAPI.cs`
- `Api/Game/GameMenu.cs`
- `GameMenuAPI.cs`
- `PolarisAPI.cs`
- 必要时只调整已有 GameMenu 暂停补丁，不新增第二组补丁。

测试项目：

- 新建 `Polaris.GameApi.Tests`，使用 xUnit，引用 `Polaris.csproj`。
- 允许通过 `InternalsVisibleTo` 测试小型状态转换规则和回调去重逻辑。
- 不为了测试复制整套游戏类；不能在测试项目重新实现另一份 API 行为。

## 5. 全局不变量

### 5.1 菜单边界

- 查询、打开、关闭游戏页面归 `PolarisAPI.Game.Menu`/`GameMenu`。
- 菜单分类注册和“菜单打开时是否暂停世界”的策略归 `PolarisAPI.GameMenu`。
- `Game.Menu.Open/TryOpen` 必须走 `NelM2DBase.menu_open` 的原版正常请求流程，不得直接调用 `UiGameMenu.activate()`。
- `Game.Menu.Current` 只在菜单真正激活后返回实例；待处理请求不算已打开。
- `Game.Menu.IsOpen` 只回答真实激活状态。
- `PolarisAPI.GameMenu.SetWorldPause(bool)` 和 `PauseWorldWhileOpen` 保留。
- `Game.World.SetPauseSimulation(bool)` 移除，不保留反向布尔语义的第二入口。

`Open/TryOpen` 的异步请求语义固定如下：

- 菜单已经打开：返回当前实例，不重复请求。
- 菜单可打开：设置 `menu_open = OPEN`，返回代表这次已接受请求的 `GameMenu` 包装器。
- 请求被接受但菜单尚未激活时，`IsOpen == false`、`Current == null`；实际打开仍以 `GameMenuOpened` 回调为准。
- 请求不可用或被原版状态拒绝：`TryOpen` 返回 `false` 且 `menu = null`；`Open` 抛出含明确原因的 `InvalidOperationException`。
- 对待处理包装器调用 `Close` 应取消请求；对已激活实例调用 `Close` 应走原版关闭流程。
- 包装器在“请求被接受 → 打开 → 关闭/取消”期间保持同一身份，关闭或取消后失效。

### 5.2 两种取消输入

- `MainMenuAPI.IsCancelInputPressed()` 继续服务标题菜单固定 `Escape/X` 规则。
- `PolarisAPI.Game.Input.WasPressed(GameInputAction.Cancel)` 继续服务游戏动作映射和玩家改键。
- 两者不得互相转发，不标记废弃。

### 5.3 Enhancer 原版事实

- 定义目录来自 `nel.ENHA.AEh`，稳定 key 来自 `ENHA.Enhancer.key`。
- 对应物品 key 为 `Enhancer_<key>`。
- 已获得状态来自 `NelItemManager.StEnhancer` 中是否存在该物品。
- `ItemStorage.ObtainInfo.top_grade` 的 bit 1（值 2）表示启用；bit 0 是收藏状态，修改启用状态时必须保留。
- 总槽位来自 `StPrecious` 中 `enhancer_slot` 的数量。
- 已用槽位是所有有效启用 Enhancer 的 `cost` 之和。
- 原版 `ENHA.fineEnhancerStorage` 会修正超出槽位的非法启用项并重建 `enhancer_bits`。
- `EnemySummoner.isActiveBorder()` 时原版 UI 禁止切换 Enhancer；公开 API 也必须报告状态拒绝。
- 状态变化后调用 `M2PrSkill.resetSkillConnectionWhole()`，不能只改库存 grade 位。

### 5.4 Skill 原版事实

- 定义目录来自 `SkillManager.getSkillDictionary()`；单项为 `PrSkill`。
- 技能书 key 为 `skillbook_<skill-key>`，不存在时 `BookItem == null`。
- `visible` 表示当前存档已获得/可见；`enabled` 是 `manip_bits` 的 bit 0。
- `manip_bits` 的 bit 1..7 对应操作选项 0..6；原版存档只能稳定保存前 6 个操作选项，不能承诺第 7 个选项跨存档稳定。
- `always_enable` 原版只限制菜单关闭按钮；v3 `IsAlwaysEnabled`/`SetEnabled` 必须明确执行“不能通过公开 API 禁用”的契约。
- `PrSkill.Obtain()` 对已经 visible 的技能不会重新启用；`Obtain(enable: true)` 必须在首次获得后显式确保启用，且不得重复发获得回调。
- `ReleaseObtain()` 不允许移除 `first_visible` 技能。
- 启用、禁用或修改操作方式后统一调用 `M2PrSkill.resetSkillConnectionWhole()`。
- `manip_multi == false` 时启用一个操作选项必须原子关闭其它选项；技能启用时不得留下零个操作选项。

### 5.5 回调与生命周期

- `GameEnhancer` 和 `GameSkill` 是目录定义包装器，身份按原版对象引用稳定；状态随当前存档变化。
- 首次观察、进入新游戏或读档后的基线建立不发变化回调，避免把存档初值误报为玩家操作。
- Polaris API 自己修改状态时可立即比较并发布；必须同步更新观察快照，防止下一帧重复发布。
- 原版菜单、事件命令或其它 Mod 直接修改状态时，由每帧差分补发。
- 回调入队顺序固定：
  - Enhancer 获得并启用：`EnhancerObtainedChanged` 后 `EnhancerActiveChanged`。
  - Enhancer 正在启用时被移除：`EnhancerActiveChanged` 后 `EnhancerObtainedChanged`。
  - Skill 获得并启用：`SkillObtainedChanged` 后 `SkillEnabledChanged`。
  - Skill 正在启用时被移除：`SkillEnabledChanged` 后 `SkillObtainedChanged`。
  - 操作方式变化只发 `SkillManipulationChanged`，载荷含 option、previous、current。
- 订阅者异常沿用现有 `GameCallbackHub` 隔离，不得阻断后续订阅者。

## 6. 反编译与文档核对规则

优先阅读：

- `C:\Users\Administrator\Documents\polarisDocs\技能系统技术文档-LLM 可读版.md`
- `C:\Users\Administrator\Documents\polarisDocs\物品系统技术文档-LLM 可读版.md`
- `C:\Users\Administrator\Documents\polarisDocs\原始.md`

需要确认具体实现时使用仓库已配置的当前游戏程序集，不把反编译产物提交进仓库：

```powershell
$aicManaged = 'D:\AliceInCradle Win ver029\AliceInCradle_ver029\AliceInCradle_Data\Managed'
ilspycmd -t nel.ENHA "$aicManaged\Assembly-CSharp.dll"
ilspycmd -t nel.PrSkill "$aicManaged\Assembly-CSharp.dll"
ilspycmd -t nel.SkillManager "$aicManaged\Assembly-CSharp.dll"
ilspycmd -t nel.UiSkillManageBox "$aicManaged\Assembly-CSharp.dll"
ilspycmd -t nel.gm.UiGMCEnhancer "$aicManaged\Assembly-CSharp.dll"
```

最低限度必须核对这些方法：

- `ENHA.initScript/Get/fineEnhancerStorage/attachEnhancer`
- `ItemStorage.getInfo/changeGradeForPrecious/getWholeInfoDictionary`
- `NelItemManager.getItem/reduceItem`
- `PrSkill.Obtain/ReleaseObtain/Show/isUseable/isManipEnable`
- `SkillManager.Get/getSkillDictionary/isObtained/isEnabled/readBinaryFrom`
- `UiSkillManageBox.fnClickCheckboxEnable/fnClickManipRow/deactivateEdit`
- `M2PrSkill.resetSkillConnectionWhole`

如果文档与当前 DLL 不同，以当前 DLL 为运行事实，并在阶段报告中记录差异；公共 API 是否随之修改仍需回到 v3 规范判断，不能直接泄漏原版签名。

## 7. 通用验证命令

建立测试项目后，每阶段至少运行：

```powershell
dotnet test Polaris.GameApi.Tests/Polaris.GameApi.Tests.csproj
dotnet build Polaris.csproj --no-restore
dotnet test Polaris.Pevt.Core.Tests/Polaris.Pevt.Core.Tests.csproj
dotnet test Polaris.Pevt.IntegrationTests/Polaris.Pevt.IntegrationTests.csproj
```

每阶段运行冲突回归扫描：

```powershell
rg -n -g "*.cs" "SetPauseSimulation|GameMenu\.Pause\(|GameMenu\.Resume\(|GameMenuAPI\.IsOpen"
rg -n -g "*.cs" "\.activate\(\)" Api/Game GameMenuAPI.cs
rg -n -g "*.cs" "MainMenuAPI\.IsCancelInputPressed|WasPressed\(GameInputAction\.Cancel"
```

第一条在冲突收敛阶段后应无生产代码命中；第二条不得出现新的菜单直接打开路径；第三条必须继续保留两类用途。

每阶段报告都列出：

1. 阶段编号和目标。
2. 有效变更行数。
3. 新增、修改、删除文件。
4. 查阅的文档和反编译类型/方法。
5. 执行过的命令及结果。
6. 阶段退出条件逐项结论。
7. 明确留给后续阶段的内容。

## 8. 阶段门与可分布执行方式

| 阶段门 | 覆盖阶段 | 必须证明的结果 |
| --- | --- | --- |
| A：契约与冲突闭环 | 1–3 | v3 公共表面被冻结，菜单只有一套实现，公共枚举和回调契约完整。 |
| B-E：Enhancer 闭环 | 4–5 | Enhancer 可查询、可修改、可观察，槽位和原版限制正确。 |
| B-S：Skill 闭环 | 6–7 | Skill 可查询、可修改、可观察，操作位和重算正确。 |
| C：生命周期闭环 | 8 | API 修改和原版 UI/事件修改都恰好发一次回调，读档不产生初值风暴。 |
| D：发布门 | 9 | 公共表面、构建、测试、运行矩阵和文档全部对齐 v3。 |

阶段 1–3 必须串行。阶段 3 完成后，阶段 4–5 与阶段 6–7 可以由两个 Claude 会话在不同工作树执行：

- Enhancer 轨只能修改第 4 节列出的 Enhancer 独占文件和对应测试。
- Skill 轨只能修改第 4 节列出的 Skill 独占文件和对应测试。
- 两条轨都不得修改 `GameEnums.cs`、三个 Callback 共享文件、`GameRuntime.cs` 或同一个测试文件。
- 合并两条轨后由单一会话执行阶段 8–9。

如果不使用独立工作树，则严格按 1→9 顺序执行。

## 9. 逐阶段实施

### 阶段 1：基线、测试项目与 v3 表面冻结（450–750 行）

- 新建 `Polaris.GameApi.Tests` 并加入解决方案，不改动 PEVT 测试项目职责。
- 建立反射式公共表面测试，至少覆盖所有 v3 新增类型、嵌套静态类、成员签名、返回类型和回调载荷。
- 将 v3 表格与边界说明整理为测试中的固定期望；测试不在运行时读取 xlsx。
- 明确表格修正项：删除 `World.SetPauseSimulation`，补入 `Menu.IsOpen` 与 `Menu.TryOpen(out GameMenu)`。
- 建立“公共签名不得出现游戏类型”的反射测试。
- 建立旧冲突 API 的负向表面测试，默认 v3 直接移除旧入口；如果项目负责人另行要求兼容，只允许 `[Obsolete]` 单行转发，不得保留第二份逻辑。
- 记录当前工作树已有改动，后续阶段只修改列出的路径。

退出条件：

- 测试项目可独立构建。
- 新增表面测试当前应按预期失败，并准确列出尚未实现的 v3 成员，而不是因为测试基础设施失败。
- v2 已实现且 v3 未删除的公共成员快照无意外变化。

### 阶段 2：菜单、暂停与取消输入冲突收敛（500–850 行）

- 把 `GameMenuAPI.Pause()` 的正常 `menu_open` 请求和状态拒绝判断迁入 `PolarisAPI.Game.Menu` 的内部实现。
- 实现 `Menu.IsOpen`、`Menu.Current`、`Menu.Open()`、`Menu.TryOpen(out GameMenu)` 的固定语义。
- 调整 `GameMenu` 生命周期，使同一包装器覆盖待处理请求和实际打开；取消或关闭后失效。
- `GameMenu.Close()` 同时正确处理待处理请求和已激活菜单，不恢复事件/转场等其它暂停来源。
- 删除 `GameMenuAPI.IsOpen/Pause/Resume` 及其独立辅助逻辑；`GameMenuAPI` 只保留分类扩展和世界暂停策略。
- 删除 `Game.World.SetPauseSimulation(bool)`。
- 保留 `GameMenuAPI.SetWorldPause(bool)`、`PauseWorldWhileOpen` 及现有四个暂停策略补丁。
- 更新 `PolarisAPI.GameMenu` XML 注释，去掉“菜单打开/关闭”的职责描述。
- 增加菜单状态矩阵测试：未进世界、不可打开、请求待处理、已经打开、不可中断状态、取消待处理、正常关闭。
- 增加取消输入表面/行为测试，证明 MainMenu 固定键与 Game.Input 动作映射没有被合并。

退出条件：

- 生产代码不存在 `SetPauseSimulation`、`GameMenuAPI.IsOpen/Pause/Resume`。
- GameAPI 内不存在直接 `UiGameMenu.activate()` 打开路径。
- 菜单打开、关闭、暂停策略各只有一个所有者。
- 阶段 1 的菜单公共表面测试通过。

### 阶段 3：共享 v3 类型、partial 接缝与回调契约（500–800 行）

- 将 `PolarisAPI.Game` 改为 `partial`，允许 Enhancer/Skill 静态入口位于独立文件，避免并行轨修改同一个大文件。
- 在 `GameEnums.cs` 定义独立公共 `GameSkillCategory` 位标志，并显式映射 `SKILL_CTG`；禁止把原版枚举直接暴露或无说明强制转换。
- 定义 `GameEnhancerActivationStatus`。至少能区分：可启用、已启用、未获得、存储/世界未就绪、槽位不足、原版状态拒绝和底层失败。
- 冻结 `SetActive` 对各状态的返回语义，并为每个枚举值写测试；不能让同一个值同时表示“查询状态”和含糊的异常。
- 新增五个 `GameInstanceCallbackKind`。
- 新增五种载荷：
  - `EnhancerObtainedChangedCallbackData`
  - `EnhancerActiveChangedCallbackData`
  - `SkillObtainedChangedCallbackData`
  - `SkillEnabledChangedCallbackData`
  - `SkillManipulationChangedCallbackData`
- 载荷至少包含对应包装器和 previous/current；操作方式载荷另含 option。
- 在 `GameCallbackContract` 同时登记种类→载荷类型和种类→实例所有者。
- 更新 `GameInstance` 文档和表面测试，但本阶段不实现真实状态读写。

退出条件：

- 所有新回调都能通过正确类型注册，错误载荷或错误实例类型立即抛异常。
- Enhancer 与 Skill 后续轨无需再修改任何共享回调文件。
- 公共枚举不依赖原版枚举数值稳定性。

### 阶段 4：GameEnhancer 目录与只读状态（500–800 行）

- 实现 `GameEnhancer` 包装器和按原版 `ENHA.Enhancer` 引用保持身份的实例表。
- 实现 `PolarisAPI.Game.Enhancers.Resolve/GetAll/SlotCapacity/UsedSlots/RemainingSlots`。
- `GetAll()` 返回只读快照，保持 `ENHA.AEh` 原版定义顺序；目录未初始化时返回空列表。
- 实现 `Key/Item/Title/Description/Cost`，关联物品只复用 `GameItem`。
- 从 `StEnhancer.getInfo(item)` 读取 `IsObtained` 和 top_grade bit 1 的 `IsActive`。
- 实现只读 `ActivationStatus`，查询时不得修改 grade、槽位或技能连接。
- 槽位查询必须处理存档未加载、`enhancer_slot` 缺失和非法旧状态；不能解析 UI 文本 `"used/max"`。
- 为 key 解析、目录顺序、空目录、本地化动态读取、槽位边界、收藏 bit 保留建立测试。

退出条件：

- v3 所有 Enhancer 只读成员和静态入口可用。
- 查询不产生任何游戏状态写入。
- `RemainingSlots == max(0, SlotCapacity - UsedSlots)`。

### 阶段 5：GameEnhancer 写操作与变化观察（600–900 行）

- 实现 `Obtain(bool notify)`：通过 `NelItemManager.getItem` 的正确高层路径获得关联物品，避免绕过 obtain_count、通知和仓库选择。
- 实现 `Revoke(bool notify)`：若启用，先原子停用并重算，再从 Enhancer 存储移除；不得误删其它仓库同 key 的异常数据。
- 实现 `SetActive(bool)`：保留收藏 bit，检查已获得、槽位、存档就绪和 `EnemySummoner.isActiveBorder()`。
- 状态变化后调用 `ENHA.fineEnhancerStorage` 和 `M2PrSkill.resetSkillConnectionWhole()`；失败时不得留下 grade 已改但连接未重算的半状态。
- `notify` 只控制原版通知表现，不影响数据修改和回调。
- 为包装器建立 obtained/active 初值快照和单次差分发布逻辑；API 自己修改后立即同步快照。
- 实现两个 Enhancer 回调的顺序、previous/current 和去重测试。
- 验证泛型库存 `ItemAdded/ItemRemoved` 与专属 Enhancer 回调可以同时存在，但各自只发一次。

退出条件：

- 获得、重复获得、启用、重复启用、停用、移除、槽位不足和战斗边界拒绝全部有确定结果。
- 回调在重算后派发且没有下一帧重复。
- 任一失败路径都不产生部分写入。

### 阶段 6：GameSkill 目录与只读状态（500–800 行）

- 实现 `GameSkill` 包装器和按 `PrSkill` 引用保持身份的实例表。
- 实现 `PolarisAPI.Game.Skills.Resolve/GetAll`；目录未初始化返回 null/空只读列表。
- 实现 `Key/BookItem/Title/Description/Category`。
- 实现 `IsVisible/IsAlwaysEnabled/IsObtained/IsEnabled`，不得使用 `M2PrSkill.isObtained(SKILL_TYPE)` 代替定义层获得状态。
- 实现 `ManipulationCount/GetManipulationText/IsManipulationEnabled`。
- option 越界行为严格按 v3：文本查询抛 `ArgumentOutOfRangeException`，布尔查询返回 false。
- 明确操作选项 0..5 可由原版格式稳定保存；option 6 即使运行时可见也不得承诺跨存档稳定。
- 为分类映射、first_visible、技能书缺失、动态本地化和操作位读取建立测试。

退出条件：

- v3 所有 Skill 只读成员和静态入口可用。
- 获得状态使用 `visible/first_visible`，启用状态使用 `enabled`，二者不混淆。
- 公共 API 不暴露 `PrSkill`、`SKILL_CTG` 或 `SKILL_TYPE`。

### 阶段 7：GameSkill 写操作与变化观察（700–1000 行）

- 实现 `Obtain(enable, notify)`：首次获得走 `PrSkill.Obtain(!enable)`；已经 visible 时不重复获得，但 `enable=true` 可按契约确保启用。
- `notify=true` 复用原版技能获得提示入口；不得通过伪造事件命令实现。
- 实现 `Revoke(notify)`：拒绝移除 `first_visible`，清除启用状态和获得状态，并统一重算。
- 实现 `SetEnabled(bool)`：未获得拒绝；`always_enable` 禁止公开 API 关闭；原版事件/编辑状态拒绝时返回 false。
- 实现 `SetManipulationEnabled(option, enabled)`：
  - 校验 option。
  - 未获得或技能未启用时拒绝修改。
  - `manip_multi=false` 时启用一个选项会原子关闭其它选项。
  - 禁止把已启用技能的最后一个操作选项关闭。
  - 修改后统一重算连接。
- 为 obtained/enabled/manipulation 建立初值快照和单次差分发布逻辑。
- 实现三个 Skill 回调的顺序、载荷和去重测试。
- 特别测试“已经获得但关闭，再次 Obtain(enable:true)”以及“原版存档 bit 7 丢失”边界。

退出条件：

- 所有 v3 Skill 写成员可用且没有半状态。
- 原版连接重算发生在回调入队之前。
- 单选、多选、最后一个选项、always_enable、first_visible 均有测试。

### 阶段 8：原版外部修改、读档与统一生命周期（600–900 行）

- 在 `GameRuntime` 统一接入 `GameEnhancerRuntime` 和 `GameSkillRuntime` 的 Pump/Sweep/Reset。
- 每帧只观察已经创建包装器或存在订阅的定义；不得为每帧重复分配完整目录快照。
- 原版 Enhancer 菜单直接改 top_grade、Skill 菜单直接改 enabled/manip_bits、事件命令 GET/REM/ENABLE/DISABLESKILL 时都能由差分捕获。
- 新游戏、读档、回标题和目录重建时重置观察基线；首次建立基线不发回调。
- API 主动修改后下一帧不得重复发布。
- 已失效包装器停止收回调；重新建立目录对象时产生新包装器身份。
- 如果轮询无法保证“重算完成后”顺序，才允许对最窄的原版提交点加 Harmony postfix；不得同时保留会重复发事件的轮询和补丁路径。
- 增加集成测试记录器，覆盖 API 路径、原版模拟路径、同帧多次写、读档基线和订阅者异常隔离。

退出条件：

- API、原版 UI、原版事件三条变化来源的回调结果一致。
- 无初值回调风暴、无双发、无跨存档旧状态泄漏。
- `GameCallbackContract`、Hub 和生命周期清理测试全绿。

### 阶段 9：表面审计、运行矩阵与发布门（400–750 行）

- 用反射生成实际公开表面，与阶段 1 固定的 v3 期望逐项比较：名称、静态/实例、参数、默认值、返回类型和可空语义。
- 复核 v3 表格：移除冲突的 `SetPauseSimulation`，补齐 `Menu.IsOpen/TryOpen`，确认 Enhancer/Skill 和五个回调与代码一致。
- 更新 `GameAPI-游戏页面与菜单边界说明.md` 的实际迁移状态；不重写已确认的五项归属。
- 运行全部构建、GameAPI 测试和 PEVT 回归测试。
- 对 public API 程序集做类型扫描，确认没有游戏内部类型泄漏。
- 在存档副本上执行最低运行矩阵：
  - 菜单请求、打开、关闭及暂停/不停世界。
  - 标题取消键与游戏改键后的 Cancel 动作。
  - Enhancer 获得、收藏 bit 保留、启用、槽位不足、战斗中拒绝、移除。
  - Skill 首次获得、重复获得、启用/禁用、always/first 限制、单选/多选操作方式。
  - 从原版菜单和事件命令修改后回调恰好一次。
  - 保存、读档后状态正确且不发初值回调。
- 输出最终差异报告，列出仍依赖 publicizer 的原版成员和游戏升级风险。

退出条件：

- v3 表面测试、单元测试、集成测试和构建全部通过。
- 冲突扫描无旧入口和第二套菜单打开逻辑。
- 运行矩阵通过；失败项不得以“仅文档问题”跳过。
- 代码、边界说明和 v3 表格三者一致。

## 10. 最终验收清单

- 游戏页面查询、打开、关闭完全归 GameAPI。
- `SetWorldPause` 与分类扩展仍归 `PolarisAPI.GameMenu`。
- 标题取消输入与游戏动作取消输入都保留且用途明确。
- `PolarisAPI.Game.Enhancers`、`GameEnhancer` 及两个回调完整实现。
- `PolarisAPI.Game.Skills`、`GameSkill` 及三个回调完整实现。
- Enhancer/Skill 关联物品复用现有 `GameItem`，没有重复物品 API。
- 所有写操作走原版真实状态结构并执行必要重算。
- API 修改与原版 UI/事件修改都能观察，且回调不漏发、不双发。
- 新游戏、读档、切换世界和关闭菜单的包装器生命周期正确。
- 公共签名不出现任何游戏内部类型。
- 当前 PEVT 工作和用户其它未提交改动未被还原、覆盖或格式化。
