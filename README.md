# Polaris

Polaris 是 Alice in Cradle 的模块化模组框架。本仓库是聚合与发行仓库；各运行时模块保存在独立 GitHub 仓库中，并以 Git submodule 固定版本。

## 模块实现状态（静态分析）

> 分析基于 2026-08-18 的当前检出，仅检查源码、项目引用、组件生命周期、Harmony 接线和测试目录，没有启动游戏做运行时验证。
> 下表的“代码行”是排除 `bin`、`obj`、`.git`、空行和纯注释后的 C# 行数；“已有主体”表示存在可执行实现，不等于已经稳定或全部验收完成。

| 模块 | 状态 | 正式源码 | 已落地内容 / 当前边界 |
| --- | --- | ---: | --- |
| `PolarisCore` | **已有主体** | 117 文件 / 12,056 行 | 唯一 BepInEx 入口；组件发现与生命周期；Harmony 补丁加载；Game API 与实例包装；静态/实例回调；菜单、设置、本地化基础设施；模组启停管理；基础错误捕获及诊断契约。 |
| `PolarisDiagnostics` | **已有主体** | 17 文件 / 2,953 行 | 错误归因与去重、责任程序集识别、报告写入、致命错误聚合、会话哨兵、主线程心跳、卡死看门狗和回调耗时记录；在 `Bootstrap` 阶段接管 Core 的诊断后端。 |
| `PolarisEvent` | **已有主体** | 125 文件 / 18,154 行 | PEVT 的文本模型、词法/语法分析、绑定与控制流诊断、编译/解释执行、异步调度、事件和人物注册、内置命令、游戏适配、Raw C#、调试页。另有 51 个测试文件、约 11,667 行测试代码，是当前测试覆盖最完整的模块。 |
| `PolarisLang` | **小型但有实质实现** | 13 文件 / 527 行 | `.plang` 解析与输出、生成代码注册接口、程序集自动扫描、按当前语言解析、原版 `TX.Get` 接入、语言切换处理和 key 冲突拦截。体量小是因为职责集中，并非空壳。 |
| `PolarisRes` | **已有主体** | 46 文件 / 2,481 行 | 目录挂载与优先级、路径沙盒、缓存和租约、导入 sidecar 配置、纹理/WAV/OGG/视频路径/PXLS 加载、静态字段自动绑定、主线程分帧泵及失败占位资源。 |
| `PolarisUI` | **已有主体** | 49 文件 / 3,425 行 | PUI 注册与生命周期、菜单接入、图状态机、热重载服务及二进制协议、图片和本地化辅助，以及标题界面、设置界面、游戏菜单的 Harmony 接线。设置数据模型和模组管理主体位于 Core，本模块主要负责游戏 UI 接入与 PUI。 |
| `PolarisSave` | **已有主体** | 32 文件 / 2,300 行 | 模组分区注册、强类型读写 API、JSON 值编解码、带版本/CRC 的尾部容器、损坏隔离和只读恢复，并通过 `COOK`/`SVD` Harmony 补丁接入新游戏、读档、序列化和安全落盘。组件入口本身没有生命周期方法，但补丁会由 Core 扫描应用。 |
| `PolarisAddons` | **仅骨架** | 1 文件 / 9 行 | 只有 `PolarisAddonsComponent` 的 `Id` 和 `Order`；没有物品、插件或技能扩展 API/运行时。 |
| `PolarisAI` | **仅骨架** | 1 文件 / 9 行 | 只有组件入口；没有 AI 定义、注册、调度或游戏接线。 |
| `PolarisMagic` | **仅骨架** | 1 文件 / 9 行 | 只有组件入口，注释也明确“后续实现”；已有原型、实现方案和落地计划文档，但没有魔法 API/运行时代码。 |
| `PolarisMap` | **仅骨架** | 1 文件 / 9 行 | 只有组件入口；没有地图格式、注册、加载或游戏接线。 |
| `PolarisParticles` | **部分实现** | 18 文件 / 1,651 行 | 已实现 `.peffect` 嵌入资源登记、分节和 `@include` 校验、批次合并、原版 `EfParticleManager` 重载，以及可按特性启用的调试协议、F9 调试页和隔离 RenderTexture 预览。当前公开 API 主要是特效文件登记，尚未落地通用的运行时生成、实例控制和作用域 API。 |
| `PolarisNetwork` | **空模块** | 0 文件 / 0 行 | 当前目录没有工程和源码，不在 `Polaris.slnx` 中，也不参与构建或部署。 |

### 汇总与已知缺口

- **有实质代码：8 个**：`Core`、`Diagnostics`、`Event`、`Lang`、`Res`、`UI`、`Save`、`Particles`；其中 `Particles` 仍是范围有限的部分实现。
- **只有可加载骨架：4 个**：`Addons`、`AI`、`Magic`、`Map`。这些工程可以生成 DLL，也能被 Core 发现，但目前没有实际业务能力。
- **完全为空：1 个**：`Network`。
- 解决方案内目前只有 `PolarisEvent` 自带测试项目；其余模块即使已有主体，也缺少仓库内自动化测试证据。
- `PolarisEvent` 的自动存档适配在 `PevtGameState` 中明确返回不支持并抛出运行时失败，其他 PEVT 主体不因此成为空壳。
- 现有功能文档声称音频支持 `.mp3`，但 `PolarisRes` 的 `AudioLoader` 实际只接受 `.wav` 和 `.ogg`；当前不能把 MP3 视为已实现。

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
