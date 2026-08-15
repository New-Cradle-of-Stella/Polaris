# Polaris

Polaris 是 Alice in Cradle 的模块化模组框架。本仓库是聚合与发行仓库；各运行时模块保存在独立 GitHub 仓库中，并以 Git submodule 固定版本。

## 获取源码

```powershell
git clone --recurse-submodules https://github.com/New-Cradle-of-Stella/Polaris.git
```

已有检出可执行：

```powershell
git submodule update --init --recursive
```

模块职责、构建和部署方式见 [`doc/PROJECT_STRUCTURE.md`](doc/PROJECT_STRUCTURE.md) 与 [`doc/README.md`](doc/README.md)。
