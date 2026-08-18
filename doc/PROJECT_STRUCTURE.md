# Polaris 项目结构

Polaris 现在由一个 BepInEx 插件核心和十一个普通 DLL 组件组成。只有 `PolarisCore.dll`
声明 `BepInPlugin`；其他程序集由核心的组件宿主发现并按生命周期驱动。

| 项目 | 输出 | 职责 |
| --- | --- | --- |
| `PolarisCore` | `PolarisCore.dll` | 唯一插件入口、公共 API、基础异常/日志捕获、组件/诊断契约、游戏绑定与基础设施；不引用任何组件项目 |
| `PolarisUI` | `PolarisUI.dll` | 模块化 UI、PUI、设置和模组管理界面 |
| `PolarisRes` | `PolarisRes.dll` | 原始资源加载、挂载、缓存与 PixelLiner 资源 |
| `PolarisLang` | `PolarisLang.dll` | `.plang` 本地化注册与解析 |
| `PolarisMagic` | `PolarisMagic.dll` | 自定义魔法能力模块；不再承载 PEVT 源码 |
| `PolarisAddons` | `PolarisAddons.dll` | 自定义物品、插件与技能扩展边界 |
| `PolarisMap` | `PolarisMap.dll` | 自定义地图能力边界 |
| `PolarisDiagnostics` | `PolarisDiagnostics.dll` | 高级诊断引擎：错误归因与去重、报告、会话哨兵、卡死看门狗及致命错误聚合 |
| `PolarisSave` | `PolarisSave.dll` | 模组存档能力边界 |
| `PolarisEvent` | `PolarisEvent.dll` | PEVT 的 Text、Syntax、Binding、Flow、静态诊断、运行时组件与内置事件内容 |
| `PolarisParticles` | `PolarisParticles.dll` | 自定义粒子与特效能力边界 |
| `PolarisAI` | `PolarisAI.dll` | 自定义 AI 行为能力边界 |

## 依赖和加载规则

- 组件不得声明 `BepInPlugin`，入口继承 `Polaris.Components.PolarisComponent`。
- `PolarisCore` 不引用任何组件项目；所有运行时组件只从外层依赖 Core。Core 在组件加载前安装 Unity、AppDomain 与 BepInEx 基础错误捕获，并提供有限缓冲和降级日志；`PolarisDiagnostics` 在 `Bootstrap` 阶段注册高级后端、接收早期错误并启用归因、报告、哨兵与看门狗。依赖方向仍为 `PolarisDiagnostics -> PolarisCore`。
- `PolarisCore` 从插件目录和 `libs` 目录加载 `Polaris*.dll`，先发现全部程序集，再按
  `Order` 调用 `Awake`、`Start`、`Update`、`LateUpdate` 和逆序 `Shutdown`。
- `PolarisEvent` 双目标构建：游戏运行时使用 `netstandard2.1` 并依赖 Core；`netstandard2.0` 兼容目标只编译纯 PEVT 前端，供 net472 的 PolarisTools 引用。
- 发布布局中 `PolarisCore.dll` 位于 `BepInEx/plugins/Polaris/`，十一个组件和第三方依赖位于
  `BepInEx/plugins/Polaris/libs/`。

## 仓库目录

- `PolarisCore` 与十一个 `Polaris*` 目录：各自指向同名私有 GitHub 仓库的 Git submodule。
- `PolarisNetwork`：已接入的 Git submodule，仓库尚无工程，暂不参与解决方案与部署。
- `PolarisCore/doc/design`：Game API 的边界说明和 v2 → v3 分阶段升级计划。
- `PolarisCore/doc/specs`：Game API v3、v4 表格规格。
- `PolarisMagic/doc/design`：PolarisMagic 原型框架与实现方案；落地计划见 `PolarisMagic/PolarisMagic-落地计划.md`。
- `PolarisEvent/doc/design`：PolarisEvent / PEVT 的设计、实施文档和阶段契约。
- `PolarisEvent/tests/PolarisEvent.Tests`：PolarisEvent 语言前端单元测试。
- `PolarisEvent/tests/Polaris.IntegrationTests`：PEVT 宿主程序集边界与跨模块集成测试。
- `doc/design`：聚合仓库级设计和实施文档。
- `doc/artifacts`：历史分析、预览和生成产物。
- `build`：共享 MSBuild 配置；部署脚本位于解决方案同级。

```powershell
git submodule update --init --recursive
dotnet build .\Polaris.slnx
.\deploy-polaris.ps1 -AicPath 'D:\Games\AliceInCradle'
.\deploy-polaris.ps1 -AicPath 'D:\Games\AliceInCradle' -Package
```
