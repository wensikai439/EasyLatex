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

API 参数以 [Chat Completions 官方文档](https://developers.openai.com/api/reference/resources/chat/subresources/completions/methods/create) 为依据：现代 OpenAI 模型采用 `max_completion_tokens`，常见旧式兼容接口保留 `max_tokens`；不强制温度参数，避免推理模型拒绝请求。不会在错误时自动重发收费请求。

发行包携带模板预热缓存。打包前核对模板源码哈希与引擎版本，并实际以 `--only-cached` 编译三个模板，避免仅凭缓存文件夹存在就声称离线可用。

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

## 0.2.0：方便、简洁与响应速度

采用用户指定的文字「设置」，保留齿轮；把双向定位放在对应左右窗格的工具栏。保留 Ctrl+J、Ctrl+点击，并加入 PDF 正文双击和一次性点选入口。定位后给出标记或源码选区。默认按用户操作跳转，编译后保留阅读位置。

### 开源实现的参考

- [TeXworks](https://github.com/TeXworks/texworks) 以简单的 TeX 文档界面为目标；本项目保留源码与 PDF 同屏的核心路径。
- [LaTeX Workshop 的 View 文档](https://github.com/James-Yu/LaTeX-Workshop/wiki/View) 提供 Ctrl+点击、可选双击、矩形标记，默认关闭编译后的自动定位；还说明频繁刷新可能使滚动位置丢失。本项目据此增加可发现的入口、位置反馈与阅读位置回归检查。
- 继续采用 [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) 的文档版本 API 映射编译时与编辑后的偏移，独立实现本项目的检查点与导航逻辑。

### 论文依据和适用范围

| 论文 | 与本项目的关系 |
| --- | --- |
| Jérôme Laurens, 2008, [Direct and reverse synchronization with SyncTeX](https://www.tug.org/TUGboat/tb29-3/tb93laurens.pdf), TUGboat 29(3), 365–371 | 使用排版引擎生成的输入文件、行号与输出位置建立双向关系。我们用真实多文件构建及官方 `synctex view/edit` 作结果比对。映射来自排版节点，部分宏或数学环境可能近似到相邻行。 |
| Chen et al., CHI 2022, [Towards Complete Icon Labeling in Mobile Applications](https://machinelearning.apple.com/research/icon-labelling), DOI 10.1145/3491102.3502073 | 研究移动界面图标及可访问性标签，支持关注可解释的入口和屏幕阅读器名称。它没有证明桌面 LaTeX 的文字按钮具有特定速度提升；可见「设置」文字遵循本项目用户要求。 |
| Jota et al., CHI 2013, [How Fast is Fast Enough?](https://www.tactuallabs.com/papers/howFastIsFastEnoughCHI13.pdf), DOI 10.1145/2470654.2481317 | 研究直接触摸指向任务中的延迟。用作减少交互阻塞、测量响应的动机；实验的触摸条件不等同桌面编辑器，未据此宣称通用毫秒阈值。 |
| [EvIcon](https://arxiv.org/abs/2305.17609), 2023 | 研究图标可用性的人工评价和探索。作为图标语义与可识别性的补充，未直接导出本项目按钮布局优于其他布局的结论。 |

paper-search 的 OpenAlex 流程需要配置邮箱；本次使用论文作者、会议、出版方的网页与论文检索，没有提交用户邮箱。论文提供设计依据，当前验证仍是工程检查；未进行正式用户可用性实验。

### 实现与取舍

SyncTeX 按源文件/行号和 PDF 页码建立索引，避免每次查询扫描所有节点。加载及建索引放入后台任务；额外加载成本与查询收益分别实测。PDF 只遍历已经实现的可见容器，按需渲染，图像缓存上限 8 页。连续缩放时保留旧图直到新图准备好。未更改文件仍校验外部改动，但跳过重复写盘；保存不再反复扫描整个项目。

[.NET 单文件部署文档](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) 说明压缩会让程序集在内存中解压，建议测量启动成本。我们比较了相同代码的压缩/不压缩自包含 EXE：启动中位数接近，不压缩版本空闲内存较少但磁盘占用更大。默认保留压缩版本，数据与复现方法见验收记录。未删减编译资源以维持三个模板首次离线可用。
