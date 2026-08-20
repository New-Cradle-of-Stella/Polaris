# Polaris

Polaris 是 Alice in Cradle 的模块化模组框架。本仓库是聚合与发行仓库；各运行时模块保存在独立 GitHub 仓库中，并以 Git submodule 固定版本。

## 模块实现状态（静态分析）

> 分析基于 2026-08-21 的当前工作区（包含尚未提交的源码），检查源码、项目引用、组件生命周期、Harmony 接线、编译结果和测试目录，没有启动游戏做运行时验证。
> 下表的“代码行”是排除 `bin`、`obj`、`.git`、`.claude`、`.codex`、`.agents`、空行和纯注释后的正式工程 C# 行数；“已有主体”表示存在可执行实现，不等于已经稳定或全部验收完成。规划文档不计作功能实现。

| 模块 | 状态 | 正式源码 | 已落地内容 / 当前边界 |
| --- | --- | ---: | --- |
| `PolarisCore` | **已有主体** | 141 文件 / 15,202 行 | 唯一 BepInEx 入口；组件发现与生命周期；Harmony 补丁加载；Game API 与实例包装；静态/实例回调；菜单、设置、本地化基础设施；模组启停管理；基础错误捕获及诊断契约；屏幕/地图 Drawing API，包含图元、路径、图片、文本、节点、Surface、地图目标跟随和后端缓存。当前未提交修改继续调整几何缓存、绘制后端、菜单暂停补丁归属并增加文本布局工具。 |
| `PolarisDiagnostics` | **已有主体** | 17 文件 / 2,953 行 | 错误归因与去重、责任程序集识别、报告写入、致命错误聚合、会话哨兵、主线程心跳、卡死看门狗和回调耗时记录；在 `Bootstrap` 阶段接管 Core 的诊断后端。 |
| `PolarisEvent` | **已有主体** | 153 文件 / 23,329 行 | PEVT 的文本模型、词法/语法分析、绑定与控制流诊断、编译/解释执行、异步调度、事件和人物注册、Raw C#、调试页；已扩展人物目录、资源声明与分组、外部事件源、晚加载/卸载/替换、热重载协议、游戏只读查询、调度、相机目标、实体移动、标准缓动及更多对话/图像/屏幕/UI/世界命令。原测试工程和测试目录已经移除。 |
| `PolarisLang` | **小型但有实质实现** | 13 文件 / 527 行 | `.plang` 解析与输出、生成代码注册接口、程序集自动扫描、按当前语言解析、原版 `TX.Get` 接入、语言切换处理和 key 冲突拦截。体量小是因为职责集中，并非空壳。 |
| `PolarisRes` | **已有主体** | 46 文件 / 2,481 行 | 目录挂载与优先级、路径沙盒、缓存和租约、导入 sidecar 配置、纹理/WAV/OGG/视频路径/PXLS 加载、静态字段自动绑定、主线程分帧泵及失败占位资源。 |
| `PolarisUI` | **已有主体** | 49 文件 / 3,507 行 | PUI 注册与生命周期、菜单接入、图状态机、热重载服务及二进制协议、图片和本地化辅助，以及标题界面、设置界面、游戏菜单的 Harmony 接线；包含基于 Core Drawing API 的自定义 PUI 控件。两个世界暂停补丁已从 UI 删除并迁移到 Core，设置数据模型和模组管理主体也位于 Core。 |
| `PolarisSave` | **已有主体** | 32 文件 / 2,300 行 | 模组分区注册、强类型读写 API、JSON 值编解码、带版本/CRC 的尾部容器、损坏隔离和只读恢复，并通过 `COOK`/`SVD` Harmony 补丁接入新游戏、读档、序列化和安全落盘。组件入口本身没有生命周期方法，但补丁会由 Core 扫描应用。 |
| `PolarisAddons` | **仅骨架** | 1 文件 / 9 行 | 只有 `PolarisAddonsComponent` 的 `Id` 和 `Order`；已有物品、Enhancer、技能、资源和存档接线的落地计划，但没有对应 API/运行时。 |
| `PolarisAI` | **仅骨架** | 1 文件 / 9 行 | 只有组件入口；已有通用 Character 行为树、Groot2/PolarisTools、热重载和调试的详细计划，但没有 AI 定义、行为树运行时、适配器或游戏接线。 |
| `PolarisMagic` | **已有主体** | 41 文件 / 2,920 行 | 已实现 `.pmagic` 文档与生成代码契约、定义/Builder/Provider、单 `RunAsync` 生命周期、时钟与同步上下文、实例/对象/图片/特效句柄、世界服务、稳定数字 ID、注册与发现、`MKind` 注入、holder 安装，以及 MDAT、选择器和魔法物品等 Harmony 接线；公开 API 支持注册特效、查询、授予和收回魔法。 |
| `PolarisMap` | **仅骨架** | 1 文件 / 9 行 | 只有组件入口；没有地图格式、注册、加载或游戏接线。 |
| `PolarisParticles` | **已有主体（仍有限制）** | 33 文件 / 2,357 行 | 已实现 `.peffect` 登记、语法/包含校验、批次合并和原版重载；运行时提供 Particle/SETTER 查询与播放、参数、稳定播放句柄、停止模式和批量 `EffectScope`；支持跟随原生对象及 Core Drawing 的 `IMapDrawTarget`，并为目标失效提供停止时间线、冻结和全部停止三种策略；另有按特性启用的调试协议、F9 页面和隔离 RenderTexture 预览。AGD 仍只能查询。 |
| `PolarisNetwork` | **空模块** | 0 文件 / 0 行 | 当前目录没有工程和源码，不在 `Polaris.slnx` 中，也不参与构建或部署。 |

### 汇总与已知缺口

- **有实质代码：9 个**：`Core`、`Diagnostics`、`Event`、`Lang`、`Res`、`UI`、`Save`、`Magic`、`Particles`。
- **只有可加载骨架：3 个**：`Addons`、`AI`、`Map`。这些工程可以生成 DLL，也能被 Core 发现，但目前没有实际业务能力。
- **完全为空：1 个**：`Network`。
- `PolarisSandbox` 已从当前聚合仓库的 `.gitmodules`、解决方案和目录中移除，因此不再作为现存模块计数。
- 当前解决方案和各子仓库均未发现自动化测试项目；`PolarisEvent` 原有测试工程已从源码和 `Polaris.slnx` 中移除。
- `PolarisEvent` 仍有明确未接通的游戏适配：自动存档模式、商店服务、实体跟随和实体动作；这些缺口不影响其编译器、运行时及大部分游戏命令已经形成主体。
- `PolarisParticles` 的 AGD（攻击残影）目前只有 `ContainsAttackGhost` 查询；通用播放只覆盖 Particle 和 SETTER。
- 现有功能文档声称音频支持 `.mp3`，但 `PolarisRes` 的 `AudioLoader` 实际只接受 `.wav` 和 `.ogg`；当前不能把 MP3 视为已实现。

### 子仓库引用状态

主仓库的 submodule 引用只能指向子仓库已经提交的 commit，不能包含子仓库中的未提交文件。本次已把工作区中的可引用版本前移如下；这些 gitlink 变更需要随主仓库的下一次提交一起提交：

| 子仓库 | 主仓库原引用 | 当前引用 | 说明 |
| --- | --- | --- | --- |
| `PolarisCore` | `dbd143d` | `ed5e5ff` | 绘制后端、文本布局工具和游戏菜单暂停处理。 |
| `PolarisEvent` | `cc99887` | `bd1bf8e` | PEVT 语言、运行时和游戏命令扩展。 |
| `PolarisUI` | `919f481` | `2f99fdc` | 删除已迁移到 Core 的世界暂停补丁。 |
| `PolarisAddons` | `17cb3d6` | `c526b43` | 增加模块落地计划，业务代码仍为骨架。 |
| `PolarisAI` | `d70b705` | `9c79bb9` | 增加行为树落地计划，业务代码仍为骨架。 |
| `PolarisMagic` | `0f80644` | `feb113b` | 提交完整的魔法定义、运行时和原版游戏接入主体。 |
| `PolarisParticles` | `06f4cd7` | `b762a2b` | 提交运行时播放 API、Drawing 目标跟随和强类型 key。 |

其余子仓库的 commit 引用没有变化。

## 获取源码

```powershell
git clone --recurse-submodules https://github.com/New-Cradle-of-Stella/Polaris.git
```

已有检出可执行：

```powershell
git submodule update --init --recursive
```

模块职责、构建和部署方式见 [`doc/PROJECT_STRUCTURE.md`](doc/PROJECT_STRUCTURE.md) 与 [`doc/README.md`](doc/README.md)。

## 统计代码行数

在仓库根目录运行：

```powershell
.\count-code-lines.ps1
```

脚本会递归统计常见源码和工程文件，默认排除 `bin`、`obj`、`.git` 等目录；运行时会显示扫描及处理进度，并分别输出总行数、空行数、纯注释行数和有效代码行数。也可指定目录、输出 JSON，或在自动化环境中关闭进度条：

```powershell
.\count-code-lines.ps1 .\PolarisCore
.\count-code-lines.ps1 -Json
.\count-code-lines.ps1 -NoProgress
```
