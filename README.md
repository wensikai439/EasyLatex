# EasyLatex

一个轻量 LaTeX 一键使用编译器。Windows 原生桌面界面，左侧写作、右侧真实 PDF 预览，并提供可选的 API AI 助手。

正在开发中，当前代码尚未达到正式发行验收。实际验证记录和已知限制见 `docs/verification.md`。

## 使用

打开 `.tex` 或项目文件夹，按 **Ctrl+Enter** 保存并编译。编译产物集中保存在项目 `.easylatex/build`。已有 TeX Live / MiKTeX 会自动检测。便携发行包附带 Tectonic 轻量引擎；首次编译需要联网获取所需宏包，以后复用本机缓存。

常用功能：多文件标签、大纲、语法高亮、代码补全、环境片段、折叠、查找替换、注释、引用键补全、错误跳转、PDF 缩放、SyncTeX、PDF 导出、中文与演示文稿模板、未保存内容恢复。

自动编译开关会在停止输入后自动保存并构建已保存的文件。手动模式始终可用。中文模板使用 XeLaTeX 和 Fandol 字体。

## AI

设置中填写兼容 OpenAI Chat Completions 的 API 根地址、模型名称和密钥。例如根地址为 `https://api.openai.com/v1`，本机模型可以使用 `http://localhost:11434/v1`。需要用户自己选择服务和模型，可能产生该服务的费用。

点击发送时才上传选区或当前文件及编译错误；不会自动上传其他项目文件。密钥保存到 Windows 凭据管理器。AI 提出可核对的修改，校验定位与重叠后由用户应用，再实际编译验证。按 Ctrl+Z 可撤销。

AI 协议可通过模拟服务验证；没有用户配置的真实 API 时，不将外部模型连接标记为已验证。

## 开发

要求 Windows 10 2004+ / Windows 11、.NET 10 SDK。

```powershell
./scripts/bootstrap.ps1       # 下载校验后的官方轻量编译引擎
./scripts/build.ps1           # 构建
./scripts/build.ps1 -Verify   # 真实编译及 WPF 界面验证
./scripts/build.ps1 -Publish  # 自包含便携包
```

开发工具在 `.tools`，构建与验证产物在 `artifacts`，都不提交到 Git。

设计依据与参考项目见 [docs/design.md](docs/design.md)。MIT 开源，第三方组件见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。
