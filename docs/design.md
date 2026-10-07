# EasyLatex 产品与实现约定

目标：Windows 本地 LaTeX 工作台，源码与 PDF 同屏，常用能力直接可达，AI 按需使用。

## 默认体验

- 打开 `.tex` 或创建模板 → 编辑 → Ctrl+Enter 保存并编译 → 在右侧看真实 PDF。
- 自动探测 TeX Live、MiKTeX；没有完整发行版时使用 Tectonic 轻量引擎。
- 输出统一放在项目 `.easylatex/build`，保持用户目录整洁。
- 工具栏只有文件、编译、自动编译、AI 和设置。大纲、诊断与 AI 可收起。
- 遇到错误保留上次成功 PDF，同时明确显示当前编译失败。
- 自动编译是可选项：开启意味着编辑暂停后自动保存并编译，开关直接说明这一行为。
- 中文字体、键盘操作、明确的焦点、屏幕阅读器名称、缩放与低分辨率窗口都属于验收范围。

## 功能范围

编辑：多文件、主文件 magic comment、语法着色、补全、括号与环境辅助、折叠、查找替换、缩进、引用键、章节大纲、撤销、恢复未保存内容。

编译：XeLaTeX/pdfLaTeX/LuaLaTeX/Tectonic、latexmk 多轮构建、BibTeX/Biber、取消、日志定位、真实 PDF、多页缩放、SyncTeX、导出。

AI：兼容 OpenAI Chat Completions 的 API，可配置根地址/模型/密钥，本机模型也可接入。用户主动发送选区或当前文件和错误；显示发送范围；返回可核对修改；唯一定位与重叠校验；用户应用后重新编译；撤销。密钥存 Windows 凭据管理器，不入设置文件、日志、Git 或诊断包。

## 参考与边界

- TeXMini（MIT，Objective-C）：清爽布局和本地工作流 https://github.com/codesun981/TeXMini
- Texmaker：高频编辑与编译能力 https://www.xm1math.net/texmaker/
- TeXworks：降低学习门槛 https://github.com/TeXworks/texworks
- AvalonEdit（MIT）：成熟 WPF 编辑组件 https://github.com/icsharpcode/AvalonEdit
- Tectonic（MIT 等）：按需获取 TeX 宏包与缓存 https://github.com/tectonic-typesetting/tectonic
- Texpile：现代本地编辑流程；AGPL 代码不纳入本项目 https://github.com/texpile/texpile
- TeXpert（SDP 2025）：复杂 LaTeX 生成仍有格式和宏包错误，因此 AI 改动须实际编译验证。论文不等同于错误修复成功率评估。https://aclanthology.org/2025.sdp-1.2/
- Microsoft Windows 设计：间距、可读性、键盘与可访问性 https://learn.microsoft.com/windows/apps/design/

本项目为独立实现；没有复制 TeXMini、Texmaker、TeXworks 或 Texpile 的源代码。
