# EasyLatex 0.1.0 验收记录

日期：2026-10-08。实际环境：Windows build 26300、.NET SDK 10.0.401、TeX Live 2025、Tectonic 0.17.0。

## 本机验证

`tests/EasyLatex.Tests` 完整检查结果：**86 项通过，0 失败**。包括真实编译、编辑操作、HTTP 模拟服务、Windows 凭据管理器、PDF 渲染和界面截图检查。日志展开和重渲染期间检查已有图像持续可见；大纲覆盖同行标签、嵌套命令和转义括号。

```powershell
./scripts/bootstrap.ps1 -WarmCache
./scripts/build.ps1 -Verify
./scripts/build.ps1 -Publish
./scripts/verify-portable.ps1
```

首启验证使用新的输出目录；可用 `-Output artifacts/another-portable-check` 指定。开发工具及验收产物统一保存在 `.tools` 和 `artifacts`，不提交用户文稿或本机设置。

| 要求 | 已检查的证据 |
| --- | --- |
| Windows 桌面体验 | 真实 WPF 窗口；浅色、深色、1024×700 窗口、AI 面板、三个设置页截图；专注模式收起与恢复。截图已逐张查看，使用说明保留一张工作区截图。 |
| 高频编辑 | 多标签关闭、保存、GBK 编码往返、外部文件变动保护、撤销恢复干净状态、查找、全部替换及撤销、注释与取消注释、数学片段合法性、嵌套折叠、大纲解析、未保存内容落盘与恢复。 |
| 编译 | 英文、中文、Beamer、多文件 BibTeX、BibLaTeX/Biber；中文和空格路径；主文件推断；进程取消；换行错误位置解析；失败后保留成功预览。完整发行版测试使用真实 TeX Live。 |
| PDF | Windows 原生渲染、三页按需预览、连续缩放、SyncTeX 正向标记与坐标往返；磁盘 PDF 被覆盖后仍导出当前成功预览的快照。 |
| 可选 API AI | 现代 OpenAI / 常见兼容接口参数、本机 HTTP 往返、认证错误、超时、取消、修改前预览、应用后真实编译、撤销、重复与重叠修改拒绝、原文已变时拒绝应用。 |
| 密钥 | Windows 凭据管理器实际写入/读取/删除；API 地址隔离；设置中删除操作绑定当前显示的服务地址。测试密钥已清理。 |
| 开箱即用 | 实际自包含 EXE 使用包内 .NET 和 Tectonic；清空工具 PATH、使用新的空缓存、阻断外网代理，默认缓存设置下三个模板全部编译并显示 PDF。 |

完整结果：`artifacts/release-verification/results.json`；日志：`artifacts/release-verification.log`；截图和各项目编译日志位于 `artifacts/release-verification/`。

## 便携包验证

程序从便携包资源种子初始化空用户缓存。两种模式均通过：

- 显式离线 `--only-cached`：英文、中文和 Beamer 均成功。
- 默认缓存设置，无可用外网代理：英文 2.2 秒、中文 1.6 秒、Beamer 1.9 秒；首启、三个模板编译及预览共 7.7 秒。结果：`artifacts/portable-default-release/result.json`。时间仅代表本机此次运行。

缓存准备脚本逐一在线预热、离线再编译三个模板。打包脚本检查模板源码 SHA256 和引擎版本，防止打包未准备完的资源。

本机保留了完整 TeX Live；便携验证明确使用包内 Tectonic，并确认运行时目录来自便携包，因此不依赖已安装的 TeX 工具或 .NET。Windows CI 还会在独立 runner 上执行便携验证。

## 自动构建与发行

[Windows Actions](https://github.com/wensikai439/EasyLatex/actions) 执行构建、单元检查、资源预热、便携打包和阻断网络的首启验证。每次生成 ZIP 和 `SHA256SUMS.txt`。

[发行页面](https://github.com/wensikai439/EasyLatex/releases) 提供 Windows x64 便携 ZIP 与校验值。仓库当前为私有，需要登录获授权的 GitHub 账号查看。

## 使用边界

- 本次没有使用付费 API 密钥调用外部模型，不把模型修复成功率或所有服务商的兼容性视为已验证。AI 修改核对后应用，通过实际编译反馈确认。
- 三个模板已经预置资源；额外宏包可能需要首次联网。Biber、LuaLaTeX 和部分复杂项目使用完整 TeX 发行版。
- 当前分发目标为 Windows x64。Windows 10 2004+ 是目标最低版本，本机实际验证为 build 26300；未覆盖所有 Windows 版本和硬件组合。
