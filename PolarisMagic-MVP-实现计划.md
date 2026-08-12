# PolarisMagic MVP 实现计划

> 目标：安装 PolarisTools 后，模组作者可以在 Visual Studio 的“添加新建项”中创建 `.pmagic`；
> 在表单里填写魔法参数，保存后自动生成定义代码和一份永不覆盖的 code-behind；作者只需实现各生命周期回调，
> 编译后的类型会在游戏启动时自动注册到 `Polaris.dll` 内的 `Polaris.Magic` 子系统（下称 PolarisMagic）。
>
> 本计划基于 2026-08-12 对 `E:\Projects\Polaris` 与同级 `E:\Projects\PolarisTools` 的只读扫描。

---

## 1. 扫描结论

### 1.1 当前工程可以直接复用的能力

- PolarisTools 已是 Visual Studio 2022+ VSIX（`net472`），已有 `.pui`、`.puisln`、`.plang` 三种项模板、
  自定义编辑器和 `IVsSingleFileGenerator`。
- `PolarisToolsPackage` 已监听“项目项新增”和“文档保存”，会自动设置 `CustomTool`、运行生成器、隐藏生成文件；
  `.pmagic` 只需作为第四种 `GeneratorBinding` 接入。
- `.pui` 已实现最关键的 code-behind 模式：
  - `Foo.pui` 生成 `Foo.g.cs`；
  - 首次保存创建 `Foo.pui.cs`；
  - 后续只补缺失的方法桩，绝不覆盖作者代码；
  - 生成类与 code-behind 用同名 `partial class` 合并。
- `.plang` 已实现“生成代码 + 特性标记 + 启动期反射扫描 + 自动注册”，不需要把源数据文件发布到游戏目录。
- Polaris 已有统一类型扫描器 `PolarisAPI.Types`，带程序集类型缓存和 `ReflectionTypeLoadException` 兜底；
  `Polaris.Magic` 不应另写一套反射扫描器。
- Polaris 当前是统一运行时库，已经同时承载游戏程序集引用、Publicizer、Harmony 补丁、API、PUI、Lang 和 Res；
  PolarisMagic 应沿用这一结构，作为同一个 `Polaris.csproj` 下的命名空间与子系统，而不是新建项目或 DLL。

### 1.2 当前缺口

- `Polaris` 与 `PolarisTools` 中没有任何可用的魔法注册表、魔法实例包装器或魔法生命周期补丁。
- 当前通用 `GameCallbackKind` 没有魔法创建、释放、命中、结束等事件，不能直接拿来实现 PolarisMagic。
- 当前还没有 `Polaris/Magic/` 源码目录和 `Polaris.Magic` 命名空间；上级两份旧方案覆盖了兼容层和节点编辑器，
  但其中“独立 PolarisMagic 工程”的假设不再采用。
- Polaris 工作树当前有一处与本计划无关的未提交修改：`Patch/Callbacks/InventoryCallbackPatches.cs`；实施时必须避开并保留。

### 1.3 本计划的范围决策

1. `.pmagic` 是 Visual Studio **项模板**，不是完整“项目模板”。首版不负责创建整个 Mod 工程。
2. 首版采用“参数文件 + C# 生命周期回调”，不要求 `.pmact` 节点图。
3. `.pmagic` 在发布时不随 Mod 分发；它只在开发期生成 C#，运行时只读取编译进 DLL 的定义。
4. 首版生成固定的生命周期回调。作者若需要法术内部的自定义状态机，先在 code-behind 中维护 enum/state；
   `.pmact` 节点编辑器作为后续阶段接入相同运行时契约。
5. PolarisMagic 的实现全部进入现有 `Polaris.csproj`，使用 `Polaris.Magic` 命名空间并随 `Polaris.dll` 发布；
   不新增同级仓库、项目引用、插件入口或运行时程序集。

---

## 2. 目标使用流程

```text
安装 PolarisTools VSIX
  → 在 Mod 项目中“添加新建项”
  → 选择 Polaris Magic Definition
  → 创建 IceLance.pmagic
  → 表单填写 ID、费用、咏唱、攻击槽、预瞄、图标、存档策略等
  → 保存
      ├─ 自动生成/更新 IceLance.pmk.cs（纯生成文件，隐藏）
      └─ 首次创建 IceLance.pmagic.cs；以后只补缺失回调，不覆盖已有代码
  → 作者在 IceLance.pmagic.cs 编写释放、每帧、命中、结束等逻辑
  → 编译 Mod DLL
  → Polaris 启动期初始化 Polaris.Magic，并扫描 [MagicAutoRegistration]
  → 校验冲突并注册定义
  → 游戏创建该魔法时，运行对应 code-behind 实例
```

目标目录关系：

```text
E:\Projects\
  Polaris\
    Api\Game\Magic\  游戏兼容层与稳定包装类型
    Patch\Magic\     Harmony 接入
    Magic\           Polaris.Magic：定义、注册表、行为运行时、文档模型
  PolarisTools\      VSIX：.pmagic 模板、编辑器、生成器、code-behind 同步
```

依赖必须单向：

```text
作者 Mod → Polaris.dll / Polaris.Magic → Polaris 内部游戏兼容层 → 游戏程序集
                         ↑
PolarisTools 仅在开发期源码链接 Polaris/Magic/Schema，不进入游戏进程
```

---

## 3. `.pmagic` v1 文件设计

建议使用 XML。原因是 `.pui`、`.plang` 已有 XML 解析与表单编辑经验，字段分组、版本迁移和错误定位都比自由 JSON 更适合本文件。

示例：

```xml
<?xml version="1.0" encoding="utf-8"?>
<PolarisMagic Version="1" Id="31001" Name="mymod.ice_lance">
  <Display
    Title="&mymod.magic.ice_lance.name"
    Description="&mymod.magic.ice_lance.description"
    SmallIcon="magic/ice_lance"
    LargeIconIndex="0" />

  <Casting MpCost="64" CastFrames="24" PrepareFrames="12" />
  <Recovery CrystalRate="0.66" NeutralRate="0.25" />

  <Attacks>
    <Attack Slot="0" Hp="10" Mp="4" Knockback="1.5" />
  </Attacks>

  <Notifier>
    <Ray Shape="Line" Length="8" Thickness="0.4" />
  </Notifier>

  <Integration
    Persistable="true"
    PreferredSlot="A"
    FollowPuzzleRules="true" />

  <LegacyIds />
</PolarisMagic>
```

v1 字段分组：

| 分组 | 字段 | 校验重点 |
| --- | --- | --- |
| Identity | `Id`、`Name` | ID 范围、Name 全局唯一、建议强制 `modid.name` |
| Display | 标题、描述、图标 | `&key` 本地化、资源引用存在性、大图标下标范围 |
| Casting | MP、咏唱帧、准备帧 | 非负、帧数上限、即时魔法与咏唱参数不冲突 |
| Recovery | 结晶/中立比例 | 0..1，禁止 NaN/Infinity |
| Attacks | Atk0..Atk2 | 槽位唯一、最多三份、伤害与击退范围 |
| Notifier | 预瞄段 | 形状和参数组合合法，咏唱前即可构建 |
| Integration | 存档、默认槽、谜题规则 | 可存档 ID 必须满足游戏存档限制 |
| Migration | `LegacyIds` | 不得含当前 ID、不得跨定义重复 |

必须在文档模型里保留 `Version`，并把 Parse、Validate、Normalize 分开：

- Parse 只回答“文件能否读懂”；
- Validate 回报字段级错误；
- Normalize 负责默认值，不允许编辑器与生成器各自猜一套默认值。

`PmagicDocument`、枚举和验证规则放在 `Polaris/Magic/Schema/`，命名空间使用 `Polaris.Magic.Schema`，
并保持为不引用 Unity、BepInEx 或游戏类型的纯 BCL 代码；PolarisTools 用现有
`<Compile Include="$(PolarisDir)\Magic\Schema\..." Link="..." />` 方式直接链接这份源码，保证编辑器和运行时共享同一语义。

---

## 4. 生成代码与 code-behind 契约

### 4.1 文件命名

| 输入/产物 | 用途 | 是否可手改 |
| --- | --- | --- |
| `IceLance.pmagic` | 参数源文件 | 是，通常由表单编辑 |
| `IceLance.pmk.cs` | 定义、特性、注册器、工厂 | 否；每次保存重建并在解决方案中隐藏 |
| `IceLance.pmagic.cs` | 生命周期 code-behind | 是；只创建一次，后续只补不存在的方法 |

生成扩展名使用 `.pmk.cs`，避免与 `.pui` / `.plang` 共用的 `.g.cs` 混淆。

### 4.2 首版固定生命周期

| 回调 | 触发时机 | 默认结果 |
| --- | --- | --- |
| `OnPrepare` | 咏唱准备圆建立；只通知，不替换原版准备圆逻辑 | 空操作 |
| `OnReleased` | 正式魔法实体建立、第一次 Tick 之前 | 空操作 |
| `OnTick` | 每个游戏逻辑帧 | `Continue` |
| `OnDraw` | 游戏要求该魔法绘制 | 空操作 |
| `OnHit` | 框架确认一次命中后 | 空操作 |
| `OnRecast` | 同种持续法术仍在场时再次施法 | `NewCast` |
| `OnEnd` | 法术结束；同一实例保证仅一次 | 空操作 |
| `OnMapDeactivating` | 离开地图前，处理退款和外部资源 | 空操作 |

在实现前的 P0 源码核对中，要逐项确认这些时机能否稳定挂接。若 `OnHit` 无法对所有攻击路径提供一致语义，
v1 应把它降为“仅框架 Ray 命中回调”，而不是承诺覆盖游戏中的所有伤害来源。

### 4.3 code-behind 形状

```csharp
using Polaris.Magic;

namespace MyMod.Magic;

public partial class IceLance
{
    protected override void OnPrepare(MagicContext context) { }

    protected override void OnReleased(MagicContext context) { }

    protected override MagicTickResult OnTick(MagicContext context, float frameScale)
        => MagicTickResult.Continue;

    protected override void OnDraw(MagicContext context, MagicDrawContext draw) { }

    protected override void OnHit(MagicContext context, MagicHit hit) { }

    protected override MagicRecastResult OnRecast(MagicWorld world, MagicCaster caster)
        => MagicRecastResult.NewCast;

    protected override void OnEnd(MagicContext context, MagicEndReason reason) { }

    protected override void OnMapDeactivating(MagicContext context) { }
}
```

### 4.4 生成文件职责

`IceLance.pmk.cs` 生成同名 partial class，并负责：

- 继承 `MagicBehavior`；
- 构造不可变 `MagicDefinition`；
- 标记 `[MagicAutoRegistration]`；
- 实现 `IMagicRegistrar.Register(MagicRegistry registry)`；
- 注册 `() => new IceLance()` 工厂，确保每个正式魔法实例拥有独立行为状态；
- 写入生成器版本和 `.pmagic` schema 版本，运行时版本不兼容时给出明确错误。

注册类实例和行为实例不能共用。扫描器只使用注册入口，运行时每次创建魔法都必须从工厂新建行为对象，
避免不同地图、不同施法实例之间串状态。

### 4.5 code-behind 同步安全规则

复用 `PuiCodeBehindSync` 的原则，但抽出通用层 `CodeBehindSync`：

- 不存在时创建完整骨架；
- 已存在时只追加缺失的方法，不改签名、不移动代码、不格式化；
- 方法查找至少同时匹配方法名和参数个数，不能继续只用 `\bName\s*\(`；
- 写入前保留原文件 BOM/换行风格；
- 追加失败只报错，不得重建或覆盖文件；
- API 回调签名变更通过编译错误和迁移说明处理，不静默改作者代码。

---

## 5. 同一 Polaris 项目内的运行时分层

### 5.1 `PolarisAPI.Game.Magic`：游戏兼容层

在 `Polaris/Api/Game/Magic/` 与 `Polaris/Patch/Magic/` 增加游戏面对层：

- 将 `MGKIND`、`MKind`、`MDAT`、`MGContainer`、`MagicItem`、选择器/存档等游戏对象封装为稳定的 Polaris 类型；
- 安装创建、准备、释放、Tick、Draw、命中、结束、切图、再施法所需 Harmony 补丁；
- 公开签名不泄漏 `Assembly-CSharp` 类型；
- 对单个下游回调异常使用 `PolarisAPI.Errors` 归因并隔离，不能让一个魔法掀掉整个游戏循环；
- `OnEnd` 做幂等，运行时实例键至少包含容器代数 + spawn serial，避免对象池复用串实例；
- 魔法 ID 和存档时序的游戏版本假设全部留在这一层。

入口使用 `PolarisAPI.Game.Magic`，稳定包装类型放在现有 `Polaris.API` 体系中，保持当前游戏能力层
“公开签名不出现游戏类型”的约定一致。这一层仍属于 `Polaris.csproj`，不形成单独程序集。

### 5.2 `Polaris.Magic`：作者与玩法层

在现有仓库中新建 `Polaris/Magic/` 目录，全部源码继续由 `Polaris.csproj` 的默认 Compile 通配编译，职责包括：

- `MagicDefinition`、`MagicAttackDefinition`、`MagicNotifierDefinition`；
- `MagicBehavior` 固定生命周期基类；
- `MagicContext`、`MagicWorld`、`MagicCaster` 等稳定作者 API；
- `MagicRegistry`、`MagicAutoRegistrationAttribute`、`IMagicRegistrar`；
- 定义校验、ID/Name/LegacyId 冲突收集、所有者程序集记录；
- 把兼容层事件调度给对应行为实例；
- 结构化日志与诊断计数。

对外门面建议命名为 `PolarisMagicAPI`，与现有 `PolarisUIAPI`、`PolarisResAPI` 的领域 API 风格一致；
例如 `PolarisMagicAPI.Registry` 提供查询与手动注册入口。生成代码通常走特性自动注册，不要求作者调用它。

自动注册复用 `PolarisAPI.Types.InPluginsWith<MagicAutoRegistrationAttribute>()`。扫描器逐类型隔离异常，
但 ID/Name 冲突不能“谁先加载谁赢”：应像 `PlangConflictGuard` 一样收集全部冲突，在扫描结束后一次性报告并阻止进入不确定状态。

### 5.3 启动时序门槛

自定义魔法必须早于游戏读取魔法选择器/存档完成登记。实施第一步必须用日志或断点确认以下相对顺序：

1. BepInEx 已发现所有作者 Mod 程序集；
2. `Polaris.Plugin` 调用 `Polaris.Magic.MagicRuntime.Init`，扫描生成的 registrar；
3. 自定义 `MKind`/ID 注入完成；
4. 游戏开始读取选择器与存档。

PolarisMagic 不拥有单独的 BepInEx 插件入口；初始化必须由现有 `Polaris.Plugin` 显式调用。
M0 需要判断它应位于 `Awake`、`Start` 还是一个更早的游戏准备补丁中。若普通 `Start` 太晚，就在 Polaris 内提供明确的
早期初始化点或对读档路径设置门控，不能靠插件加载顺序碰运气。作者 Mod 的运行时硬依赖仍然是 Polaris，而不是一个不存在的 PolarisMagic DLL。

---

## 6. PolarisTools 改造清单

### 6.1 项模板与打包

新增：

```text
ItemTemplates/Polaris/PmagicFile/
  PmagicFile.vstemplate
  Template.pmagic
```

并在 `PolarisTools.csproj` 中新增 `VSIXSourceItem`。模板默认名建议 `NewMagic.pmagic`，模板分组沿用 `Polaris`。

### 6.2 编辑器

新增 `Magic/PmagicEditor/`：

- `PmagicEditorFactory` / `PmagicEditorPane`；
- WPF 表单与 ViewModel；
- 分组页：身份、展示、咏唱、攻击槽、预瞄、集成；
- 数值、枚举、资源、本地化 key 使用现有 PUI 小控件和 `PlangKeyCatalog`；
- 保存前显示字段级错误，错误同时进入 VS Error List；
- 提供“打开 code-behind”和“重新生成缺失回调”按钮。

首版不做节点画布，也不做魔法热重载。

### 6.3 单文件生成器

新增具有独立 GUID 的 `PolarisMagicGenerator : IVsSingleFileGenerator`：

- `GeneratorName = "PolarisMagicGenerator"`；
- `DefaultExtension = ".pmk.cs"`；
- 解析/验证失败时用 `IVsGeneratorProgress.GeneratorError` 写入错误列表；
- 代码发射器保持纯函数，VS COM 外壳只负责字节输出，便于做快照测试；
- 全限定名使用 `global::`，字符串统一走共享的 C# literal 转义；
- 输出应具有确定性，相同输入必须字节一致。

### 6.4 Package 接入

在 `PolarisToolsPackage` 中：

- 添加 `[ProvideCodeGenerator]`；
- 添加 `[ProvideEditorFactory]`、`[ProvideEditorExtension(".pmagic")]`；
- 在 `GeneratorBindings` 添加 `.pmagic → .pmk.cs`；
- `BeforeGenerate` 调用 `PmagicCodeBehindSync`；
- 初始化时注册 editor factory；
- 文档保存后沿用现有自动 `RunCustomTool` 和隐藏生成文件流程。

---

## 7. 实施里程碑

### M0：游戏源码与时序验证

交付：一份已核实的魔法生命周期映射表、需要的补丁列表、注册早于读档的证据。

验收：每个计划回调都有明确的游戏入口、调用次数、准备态/正式态语义；无法稳定支持的回调从 v1 契约删除或收窄。

### M1：Polaris 低层魔法桥

交付：魔法实例包装、生命周期事件、实例身份、异常隔离；先只观察原版魔法，不创建自定义魔法。

验收：用原版至少两种结构不同的魔法记录 Prepare → Released → Tick/Draw → Hit → End 顺序；结束不重复、切图不泄漏。

### M2：PolarisMagic 手写注册 MVP

交付：在现有 `Polaris.csproj` 中加入 `Polaris.Magic` 命名空间、定义模型、注册表、基类、扫描器和冲突守卫；
由 `Polaris.Plugin` 初始化，暂时由测试 Mod 手写定义。

验收：一个最小即时魔法能注册、生成、每帧运行并正常结束；两个 Mod 撞 ID/Name 时稳定拒绝而不是随加载顺序覆盖。

### M3：咏唱、再施法与存档

交付：准备态/正式态双路径、选择器接入、Persistable、LegacyIds、存档边界检查。

验收：正常释放、中断咏唱、再次施法、切图、存读档、卸载/重装定义 Mod 都有明确且可重复的结果。

### M4：`.pmagic` schema 与生成器

交付：共享 `PmagicDocument`、验证器、纯代码发射器、`PolarisMagicGenerator`、快照测试。

验收：同一 `.pmagic` 稳定生成可编译的 `.pmk.cs`，生成类可被 M2 扫描并注册；坏输入在 VS Error List 中定位到具体字段。

### M5：模板与 code-behind

交付：VSIX 项模板、`.pmagic.cs` 骨架、缺失回调同步、Package 自动绑定。

验收：干净 VS 实验实例中“新建 → 保存 → 两个产物出现 → 第二次保存不覆盖作者代码 → 编译成功”全流程通过。

### M6：表单编辑器与作者体验

交付：完整表单、资源/本地化补全、即时校验、README、示例 IceLance。

验收：作者不手改 XML 也能创建合法魔法；从安装 VSIX 到游戏内看到自定义魔法不需要手工设置 CustomTool 或手工调用 Register。

### M7：后续扩展（不阻塞首版）

- `.pmact` 两层节点图；
- 动作图热重载；
- 当前阶段/变量实时探针；
- 第三方节点目录；
- 完整 Mod 项目模板。

---

## 8. 测试矩阵

### 8.1 纯代码测试

- XML v1 正常解析、默认值、未知字段、未来版本拒绝；
- 所有数值边界、NaN/Infinity、重复攻击槽、重复 LegacyId；
- 文件名/命名空间到合法 C# 标识符的转换；
- C# 字符串与 XML 特殊字符转义；
- 生成结果快照与确定性；
- code-behind 已存在、部分方法已存在、同名重载、CRLF/LF、UTF-8 BOM；
- 注册冲突、无公开构造函数、抽象类、错误类型被跳过并继续扫描。

### 8.2 VSIX 集成测试

- 安装到 Experimental Instance；
- “添加新建项”可见 `.pmagic`；
- 自动设置 CustomTool；
- 保存时生成 `.pmk.cs` 和 `.pmagic.cs`；
- 生成文件隐藏/嵌套正确；
- code-behind 永不被覆盖；
- 错误进入 Error List，修复后可重新生成。

### 8.3 游戏端测试

- 即时魔法、咏唱魔法、持续魔法各一份；
- 多实例同时存在不串状态；
- 再施法、命中、自然结束、主动 kill、切图、读档；
- 回调抛异常时只禁用/结束该实例，并能归因到作者程序集；
- ID/Name/LegacyId 冲突；
- 存档含已卸载魔法时给出可恢复行为，不把 ID 静默解释成别的魔法；
- 每帧路径做分配与耗时检查，避免 Tick 引入 LINQ、闭包或反射。

---

## 9. 发布门槛与完成定义

首个可发布版本必须同时满足：

1. 安装 PolarisTools 后能从“添加新建项”创建 `.pmagic`；
2. 保存自动生成 `.pmk.cs` 与一次性 `.pmagic.cs`；
3. 作者代码二次保存、重开 VS、升级 VSIX 后不丢失；
4. Mod 项目只需引用 `Polaris.dll` 并使用 `Polaris.Magic`，不需要额外的 PolarisMagic DLL，也不需要手写注册调用；
5. 冲突和非法存档 ID 不依赖加载顺序，启动时给出明确错误；
6. 至少一个咏唱魔法和一个持续魔法通过完整生命周期测试；
7. 生成器、运行时和文档中的回调签名完全一致；
8. 不把 `.pmagic` 数据文件作为运行时依赖发布。

建议的首版边界是 M0–M6。节点编辑器、热重载与完整项目模板放到 M7，不阻塞代码式魔法工作流上线。
