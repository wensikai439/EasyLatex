# 外部 AI 工具接入

适用于 0.3.0-preview.1 及后续预览版。旧的 0.2.2 便携包没有此接口。

## 开启

打开 EasyLatex，在「设置 → AI 助手」勾选「允许本机 AI 工具读写文档」，保存设置。选择客户端后点击「复制接入配置」，路径会按当前 EasyLatex 所在文件夹生成。EasyLatex 保持打开即可；无需给它填写外部客户端的 API 密钥。

接口默认关闭，只接受同一 Windows 用户在本机的命名管道连接。外部客户端使用自身的模型和授权；文档是否发送给模型服务，由该客户端决定。EasyLatex 的接口不返回应用内保存的 API 密钥。

## Codex

最简单的命令如下，将路径替换为当前便携包中 EXE 的实际路径：

```powershell
codex mcp add easylatex -- 'D:\EasyLatex-portable\EasyLatex.exe' --mcp
```

也可将设置中复制的 TOML 段加入 Codex 的配置。参考 [Codex MCP 官方文档](https://developers.openai.com/codex/mcp)。

## Claude Code

```powershell
claude mcp add --transport stdio easylatex -- 'D:\EasyLatex-portable\EasyLatex.exe' --mcp
```

设置中复制的是 `mcpServers` JSON，可用于项目 `.mcp.json`；已有配置时只合并 `easylatex` 项，保留其他服务器。参考 [Claude Code MCP 文档](https://code.claude.com/docs/en/mcp)。

## DeepSeek Harness（dsh）

本机核对版本为 `@deepseek-ai/dsh 0.1.7-rc.2`。选择 DeepSeek Harness，复制配置并保存为 `easylatex.patch.yml`，其结构为：

```yaml
- insert:
    - id: mcp-easylatex
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: easylatex
        transport: stdio
        command: 'D:\EasyLatex-portable\EasyLatex.exe'
        args: ['--mcp']
```

在你通常使用的 profile 上增加 `--patch .\easylatex.patch.yml`。例如使用 web profile：

```powershell
dsh --profile web --patch .\easylatex.patch.yml
```

该插件在当前 dsh 中随安装提供，工具将以 `mcp__easylatex__工具名` 出现。参考 [DeepSeek 官方 MCP 插件说明](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/mcp/mcp-client/README.md)。本机已经用 dsh 随附的官方 SDK 2.0 验证自动协议协商、工具发现、读取及修改；尚未启动模型进行端到端任务评估。

## WorkBuddy

在 WorkBuddy 的 MCP 配置中添加设置里复制的 JSON，使用 `type: stdio`、EXE 路径和 `args: ["--mcp"]`。不用声明 Node runtime。已有配置时合并服务器项。参考 [WorkBuddy 官方连接器说明](https://open.workbuddy.cn/docs/connector)。不同版本的设置入口可能不同，本次未实际启动 WorkBuddy 联调。

## 可调用的能力

| 工具 | 能力 |
| --- | --- |
| `list_sessions` | 找到已经开启接口的 EasyLatex 窗口 |
| `list_documents` | 读取打开的文档、当前文档与版本 |
| `get_document` | 分段读取源码，单次最多 100,000 个 UTF-16 字符 |
| `apply_edits` | 按版本应用局部修改，一个请求作为一次撤销；支持协作同步 |
| `insert_block` | 在当前文档插入摘要、章节、公式、三线表或讲义模块；表格行数含表头 |
| `compile` | 保存并开始编译已有路径的文档，立即返回；随后读取诊断 |
| `get_diagnostics` | 查看编译状态、错误与警告，可选日志末尾 |

修改前必须读取最新版本；版本过期、范围重叠或拆开 Unicode 字符时，整个修改请求会被拒绝。输入法组词期间暂不接受外部修改。编译接口会保存已命名文档，拒绝未命名文档、外部文件冲突及开启 shell-escape 的设置。普通编辑不会因工具调用而切换当前文档；便捷模块限当前文档。

可以向外部 AI 这样描述任务：

> 通过 EasyLatex 读取当前文档和诊断，修复导致编译失败的 LaTeX 语法，保留正文意思。按最新版本应用修改，编译后检查错误是否消失。

接口不会保证模型修改正确；实际编译和用户核对仍是结果判断依据。关闭本机接口开关后，会话停止接受新请求。主界面没有新增外部工具面板或调试信息。
