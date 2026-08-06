# TestRig CLI

TestRig CLI 是面向 Windows/.NET 8 的测试工装脚手架。它将 `Template/Common` 公共 WPF 骨架与选定的业务模板合并，替换项目令牌、写入版本与审计信息、生成可选发布脚本，并在发布输出前完成编译验证。

当前可生成的完整业务模板为 `Machine`。`Complete`、`Inspect`、`Aging` 和 `Dynamic` 暂为禁用预留模板，待各自补齐前端业务逻辑与 WPF UI 后再启用。

## 环境与安装

要求 Windows 10/11 和 .NET 8 SDK。混淆和安装包功能分别依赖 Obfuscar 与 Inno Setup。

从源码运行：

```powershell
dotnet run --project .\WPFCli\WPFCli.csproj
dotnet run --project .\WPFCli\WPFCli.csproj -- --help
```

打包并安装为本地工具：

```powershell
dotnet pack .\WPFCli\WPFCli.csproj -c Release -o .\artifacts
dotnet tool install --global --add-source .\artifacts TestRigCli --version 2.0.0
testrig-cli --list-templates
```

NuGet 工具包包含完整的 `Template` 目录，因此安装后的命令可在仓库外运行。需要使用自定义模板时，通过 `--template-root` 显式指定目录。

## 使用方法

无参数启动交互式向导：

```powershell
testrig-cli
```

非交互生成：

```powershell
testrig-cli --biz machine --code PT01
testrig-cli --biz machine --code PT02 --version 2.3.0 --output D:\Projects\PT02
testrig-cli --biz machine --code PT03 --dry-run --no-build
```

| 参数 | 说明 |
| --- | --- |
| `--biz <类型>` | 业务模板，必填；名称从模板目录自动发现 |
| `--code <项目代号>` | 2-20 位字母数字，必须以字母开头 |
| `--list-templates` | 列出发现的业务模板及启用状态 |
| `--template-root <目录>` | 使用指定的模板根目录 |
| `--output <目录>` | 指定输出目录，默认 `Output/<项目代号>` |
| `--version <版本>` | 指定 `major.minor.patch[.revision]` 版本 |
| `--force` | 完整构建成功后替换已有输出 |
| `--dry-run` | 在临时目录完成生成和自检，不发布输出 |
| `--no-build` | 生成但不执行 `dotnet build` |
| `--obfuscate` / `--no-obfuscate` | 是否生成混淆流程，默认关闭 |
| `--pack` / `--no-pack` | 是否生成安装包流程，默认关闭 |
| `--gitlab <URL>` | 生成 GitLab CI 与推送脚本，仅接受 HTTP/HTTPS URL |
| `--ftp-host <地址>` | 生成 FTP 发布流程，仅接受 FTP/FTPS URL |
| `--ftp-dir <目录>` | FTP 远程目录，必须与 `--ftp-host` 同时使用 |
| `--help` / `-h` | 显示帮助 |

## 生成保证

一次构建的模板合并、令牌替换、版本写入、脚本生成和项目编译全部发生在最终输出的同级随机暂存目录中。只有所有阶段成功后才发布结果；失败和 `--dry-run` 都不会修改已有输出。覆盖已有目录必须使用 `--force`。

以下输出目标会被拒绝：磁盘根目录、Git 仓库、链接目录、文件占用路径，以及与模板目录互相包含的路径。模板自身也不能包含文件或目录链接。

默认版本是 `Template/Common/Directory.Build.props` 基线版本的 patch 加一，也可通过 `--version` 固定指定。生成过程永远不会写回模板基线，因此失败、预演和重复生成都不会悄悄改变仓库版本。

## 模板分层

`Common` 只包含跨业务共享的非 WPF 基础：核心抽象与引擎、基础设施、设备通信、测试步骤、治具和 DeviceLink。它有一个不包含应用层的 `PCBA.sln`，用于单独验证公共层。

`Machine` 是当前完整的整机测试通用模板，额外包含 `src/07.UI` 前端业务逻辑与视图、`src/08.App` WPF 启动项目，以及覆盖公共层解决方案的 `PCBA.sln`。合并生成后，输出项目仍包含完整的公共层和整机前端。

其他业务目录只保留配置和说明，必须在补齐自己的前端业务逻辑、UI、App 和解决方案后，才应将 `disabled` 改为 `false`。

## 模板配置

根配置为 `Template/template.config.json`。`schemaVersion` 当前必须为 `1`，未知字段、绝对路径、`..` 路径和不安全配置都会直接失败。

```json
{
  "schemaVersion": 1,
  "placeholder": "PCBA",
  "description": "TestRig 多业务模板脚手架",
  "targetFramework": "net8.0-windows10.0.19041.0",
  "configuration": "Release",
  "mainProjectName": "PCBA.App",
  "excludeFromCopy": ["bin", "obj", ".vs", ".git"],
  "excludeFromReplacement": ["src/libs/DeviceLink", "tools", "docs"],
  "deleteFromOutput": [],
  "obfuscationTargets": ["Infrastructure", "Devices"],
  "reservedNames": ["PS02"]
}
```

每个 `Template/<BusinessName>/template.config.json` 至少声明：

```json
{
  "schemaVersion": 1,
  "description": "业务模板说明",
  "businessType": "businessname",
  "disabled": false,
  "excludeFromCopy": [],
  "excludeFromReplacement": [],
  "deleteFromOutput": []
}
```

`businessType` 必须与目录名忽略大小写后完全一致。业务配置中的排除和删除规则与根配置合并。`deleteFromOutput` 可在公共模板合并后删除指定的安全相对路径。

新增业务模板只需创建目录及配置文件；CLI 和交互向导会自动发现，不需要修改硬编码列表。业务模板先覆盖同相对路径的公共文件，再执行删除、替换和重命名。

## 模板令牌

推荐在文本内容、文件名和目录名中使用显式令牌：

| 令牌 | 值 |
| --- | --- |
| `{{ProjectCode}}` / `{{ProjectName}}` | 项目代号 |
| `{{MainProjectName}}` | 替换后的主项目名，例如 `PT01.App` |
| `{{RootNamespace}}` | 项目代号 |
| `{{Version}}` | 本次生成版本 |
| `{{BusinessType}}` | 业务类型 |
| `{{TargetFramework}}` | 根配置中的目标框架 |

旧模板中的字面量 `PCBA` 仍兼容替换。替换仅处理明确允许的 UTF-8 文本类型，并保留原 BOM；未知类型或非 UTF-8 文件按二进制原样复制。生成结束后若内容或路径仍残留未知 `{{Token}}` 或 `PCBA`，构建会失败。

## 依赖与安全默认值

模板依赖版本集中在 `Template/Common/Directory.Packages.props`，项目文件不再内联包版本。公共 WPF 项目统一到 `net8.0-windows10.0.19041.0`；DeviceLink 有意保留 `net6.0` 和 `netstandard2.0`，后续将作为 GitHub 子模块独立维护。

生成项目不会预置内网 OA/API 地址，真实硬件默认关闭，未配置的外部 API 客户端会短路返回。登录历史只保存用户名，不持久化或恢复密码。FTP 凭据也不写入脚本和 Git 历史，发布时从以下环境变量读取：

```powershell
$env:TESTRIG_FTP_USER = "your-user"
$env:TESTRIG_FTP_PASSWORD = "your-password"
```

## 项目结构

```text
WPFCli/
├── WPFCli/                       # CLI、模板目录定位和构建流水线
├── WPFCli.Tests/                 # 配置、解析、生成和事务回归测试
├── Template/
│   ├── template.config.json      # 根配置
│   ├── Common/                   # 公共后端基础层与公共解决方案
│   ├── Machine/                  # 整机前端业务逻辑、WPF UI、App 和完整解决方案
│   ├── Complete/                 # disabled，待补齐自己的前端层
│   ├── Inspect/                  # disabled，待补齐自己的前端层
│   ├── Aging/                    # disabled，待补齐自己的前端层
│   └── Dynamic/                  # disabled 预留模板
└── .github/workflows/ci.yml      # Windows 构建、打包和安装冒烟测试
```

## 验证

```powershell
dotnet test .\WPFCli.Tests\WPFCli.Tests.csproj --nologo
dotnet build .\Template\Common\PCBA.sln --nologo -m:1
dotnet build .\Template\Common\src\libs\DeviceLink\DeviceLink.sln --nologo -m:1
dotnet pack .\WPFCli\WPFCli.csproj -c Release -o .\artifacts --nologo
```

CI 还会把打出的工具包装到隔离目录，并从仓库外执行 `--list-templates` 与 `--dry-run`，以验证打包模板确实可用。
