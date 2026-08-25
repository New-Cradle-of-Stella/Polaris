# Polaris

个人娱乐项目，纯 vibe coding。

Polaris 是 Alice in Cradle 的模块化模组框架。本仓库是聚合与发行仓库；各运行时模块保存在独立 GitHub 仓库中，并以 Git submodule 固定版本。

## 模块职责

| 模块 | 负责内容 |
| --- | --- |
| `PolarisCore` | 框架唯一的 BepInEx 入口，负责组件生命周期、游戏对象封装、菜单与设置、本地化、基础诊断以及屏幕和地图绘制能力。其它模块通过 Core 提供的稳定 API 接触游戏。 |
| `PolarisDiagnostics` | 收集并归因异常，生成错误报告和会话记录，同时监视主线程心跳、卡死与致命故障。 |
| `PolarisEvent` | 提供 PEVT 事件语言及其解析、诊断、执行和异步调度，并把对话、镜头、实体、世界状态等事件命令接入游戏。 |
| `PolarisLang` | 负责 `.plang` 多语言内容的注册、解析和原版文本查询接入，并处理语言切换与 key 冲突。 |
| `PolarisRes` | 从模组目录加载图片、音频、视频路径和 PixelLiner 资源，管理挂载优先级、缓存、租约及静态字段绑定。 |
| `PolarisUI` | 提供 PUI 界面、状态图、菜单集成和热重载，并承载框架自己的标题、设置及模组管理界面。 |
| `PolarisSave` | 为模组提供独立的强类型存档分区，将带版本和校验信息的数据安全附加到原版存档。 |
| `PolarisAddons` | 定义并运行自定义物品、插件和技能，负责内容目录、依赖注入、状态与数值修改，以及向原版系统的投影。 |
| `PolarisAI` | 提供 `.pai` 行为树、Actor 控制和 NPC 创建能力，并支持 `.pnpc` 自定义 NPC、行为热重载及原生 AI 接管。 |
| `PolarisMagic` | 定义、注册和执行自定义魔法，管理魔法实例、对象、特效、同步上下文以及原版魔法目录接线。 |
| `PolarisMap` | 创建和编辑地图，编译 `.pmap`/TMAP 数据，处理地图写盘、切换、热重载与调试查看。 |
| `PolarisParticles` | 注册和播放自定义粒子特效，管理参数、播放句柄、目标跟随、批量生命周期和调试预览。 |
| `PolarisNetwork` | 作为网络与联机扩展的模块边界。 |

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
