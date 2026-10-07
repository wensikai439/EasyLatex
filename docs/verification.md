# EasyLatex 验收记录

## 0.2.0：双向导航与性能

日期：2026-10-08。环境与下方 0.1.0 记录一致。新增检查涵盖可见「设置」文字、左右定位按钮、双击/Ctrl 点击触发条件、Esc 取消、多文件主文件、与官方 SyncTeX CLI 的页码及文件比对、插入未保存行后的正反向映射、撤销、重开文件版本校验、空白点击、40 页预览、连续缩放、8 页图像缓存上限、容器虚拟化、保留重新编译前的阅读位置、干净文件不重复写盘、渲染期间重载及损坏 SyncTeX 时 PDF 仍可预览。

最终完整检查 **113 项通过、0 失败**；独立定位基准及一致性检查 **4 项通过、0 失败**。正式结果与日志：`artifacts/release-verification-0.2.0/results.json`、`artifacts/release-verification-0.2.0.log`、`artifacts/release-performance-0.2.0/`。异常测试曾发现损坏压缩映射会中断加载，修复后完整重跑通过。

```powershell
$env:DOTNET_ROOT = (Resolve-Path .tools/dotnet).Path
./.tools/dotnet/dotnet.exe run --project tests/EasyLatex.Tests -c Release -- artifacts/release-verification-0.2.0 --ui
./.tools/dotnet/dotnet.exe run --project tests/EasyLatex.Tests -c Release -- artifacts/release-performance-0.2.0 --perf-only
./scripts/build.ps1 -Publish -OutputName EasyLatex-0.2.0-win-x64
./scripts/verify-portable.ps1 -AppPath artifacts/EasyLatex-0.2.0-win-x64/EasyLatex.exe -Output artifacts/portable-0.2.0
```

### 测量结果

十万 SyncTeX 节点、10 个文件、500 页的合成基准：预热 10 次，测量 100 次；使用同一文档对照优化前后。随机查询还与原算法比对 100 次。

| 指标 | 原实现 | 索引后 |
| --- | --- | --- |
| 正向查询 P50 / P95 | 20.58 / 23.84 ms | 0.0005 / 0.0006 ms |
| 反向查询 P50 / P95 | 0.610 / 0.774 ms | 0.0036 / 0.0044 ms |
| 反向查询每次线程分配 | 6,920 B | 0 B |
| 读取与建表 | 359 ms | 390 ms |

以上仅测算法查询，排除 UI 滚动和 PDF 渲染，亚毫秒数据不能当作跨机器承诺。索引多花约 31 ms 建表时间，已移到后台。原始记录在 `artifacts/optimization-baseline/performance.json` 和 `artifacts/final-sync-performance/performance.json`。

真实 40 页文档做 12 次跨页跳转，并在每次跳转后连续放大/缩小：页面图像准备时间 P50 46.7 ms、该轮最大 81.3 ms（含 30 ms 轮询分辨率）。结果采样时缓存 3 页、实现容器 2 个；所有步骤均检查缓存不超过 8 页。该数字来自本机一次运行，未覆盖复杂图片文档与所有硬件。原始记录：`artifacts/usability-check/stress-performance.json`。

相同源码的两种自包含包，各独立进程启动 5 次，隔离设置，启动后 1.5 秒采样内存。测量包含从启动到 UI 空闲就绪文件的时间，轮询分辨率 20 ms；未清空操作系统文件缓存，因此属于本机重复启动比较。

| 打包方式 | ZIP | 解压目录 | 启动中位数 | 空文档工作集中位数 |
| --- | --- | --- | --- | --- |
| 压缩 EXE（默认） | 约 117.6 MiB | 约 190.1 MiB | 0.981 s | 333.5 MiB |
| 不压缩 EXE（试验） | 约 119.6 MiB | 约 301.8 MiB | 0.984 s | 241.2 MiB |

启动时间接近；默认保留较小的解压占用。不压缩版内存更少的原因与程序集映射/解压有关，未宣称减少全部文档运行内存。工作集会随 Windows 回收、字体、页面尺寸等变化。完整检查进程包含多个窗口、编译和截图，不能作为实际单窗口内存指标。

复现打包比较：`build.ps1 -Publish -OutputName <新目录名>`，第二种再加 `-NoCompression`；使用 `measure-startup.ps1 -AppPath <EXE> -Output <新输出目录> -Runs 5`。原始记录在 `artifacts/startup-compressed/startup.json` 和 `artifacts/startup-uncompressed/startup.json`。

设计参考、论文依据及范围说明见 [design.md](design.md)。新版本放在独立目录，没有覆盖用户正在运行的 0.1.0 或修改其文稿。Windows x64、内置三模板离线编译和外部 AI 的适用边界仍按下方记录。

## 0.1.0

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
