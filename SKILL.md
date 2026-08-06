---
name: testrig-cli
description: 使用或维护 TestRig CLI 多业务测试工装脚手架，包括生成项目、修改 Template、校验 template.config.json、扩展生成流水线，以及生成 GitLab/FTP 发布方案。
tools: Read, Write, Edit, Bash, Grep, Glob
version: 3.0.0
---

# TestRig CLI 技能

## 适用范围

在当前仓库中处理以下任务时使用本技能：

- 从 `Common + Machine` 生成当前完整的整机测试工装项目
- 修改 CLI 参数、交互向导、模板目录或模板配置
- 添加业务模板或模板令牌
- 调整版本、混淆、安装包、GitLab 或 FTP 生成流程
- 排查模板生成、自检或编译失败

普通 WPF/MVVM 问题且与本脚手架无关时不使用。

## 首要规则

1. 从仓库根目录执行命令，不依赖固定盘符。
2. 先运行 `testrig-cli --list-templates` 或读取 `TemplateCatalog`，不要维护业务类型硬编码列表。
3. 模板配置必须通过 `TemplateCatalog` 加载；不要用宽松 JSON 反序列化绕过 schema 和路径校验。
4. 生成过程不得修改 `Template/Common/Directory.Build.props` 的版本。
5. 不把密码、令牌、真实内网 URL 或真实硬件默认开关写入模板。
6. 修改公共模板后至少构建 `PCBA.sln`；修改 DeviceLink 后还要构建 `DeviceLink.sln`。
7. `Common` 只放跨业务公共层；业务前端逻辑、WPF UI、App 和完整解决方案放到对应业务目录。
8. `Complete/Inspect/Aging` 当前为 disabled 预留模板，不要在没有完整前端层和解决方案时启用。

## 常用命令

```powershell
# 交互模式
dotnet run --project .\WPFCli\WPFCli.csproj

# 查看自动发现的模板
dotnet run --project .\WPFCli\WPFCli.csproj -- --list-templates

# 非交互生成
dotnet run --project .\WPFCli\WPFCli.csproj -- --biz machine --code PT01

# 仅验证生成，不发布输出
dotnet run --project .\WPFCli\WPFCli.csproj -- --biz machine --code PT01 --dry-run --no-build

# 使用外部模板和自定义输出
dotnet run --project .\WPFCli\WPFCli.csproj -- --template-root D:\Templates --biz machine --code PT01 --output D:\Projects\PT01 --version 2.0.0
```

可用参数包括 `--template-root`、`--output`、`--version`、`--force`、`--dry-run`、`--no-build`、`--list-templates`、混淆/打包开关，以及 GitLab/FTP 配置。以 `--help` 的实时输出为准。

## 构建模型

`BuildPipeline` 负责完整事务：

1. 校验输出路径与环境。
2. 从公共模板读取版本基线；默认 patch 加一，或采用 `--version`。
3. 在最终输出同级的随机暂存目录中执行所有阶段。
4. 合并 `Common` 与业务模板。
5. 应用 `deleteFromOutput`、内容替换和路径重命名。
6. 执行占位符与关键产物自检。
7. 写入生成项目版本与审计元数据。
8. 生成可选发布、混淆和打包脚本。
9. 除 `--no-build`/`--dry-run` 外，编译生成的解决方案。
10. 全部成功后才替换最终输出；失败时保留旧输出。

不要在流水线之外提前删除或覆盖输出。`--force` 只表示允许在成功时替换已有输出。

## 模板合并

模板目录结构：

```text
Template/
├── template.config.json
├── Common/
├── Complete/template.config.json  # disabled 预留
├── Machine/                       # 完整整机模板：前端层、App、PCBA.sln
├── Inspect/template.config.json   # disabled 预留
├── Aging/template.config.json    # disabled 预留
└── Dynamic/template.config.json
```

公共模板先复制，业务模板随后按相对路径覆盖。根配置与业务配置的 `excludeFromCopy`、`excludeFromReplacement` 和 `deleteFromOutput` 取并集。配置路径必须是模板内的相对路径，不能包含 `..` 或根路径。当前 Machine 的 `PCBA.sln` 会覆盖 Common 的公共层解决方案，生成完整整机项目。

模板元数据不会进入输出。链接文件和链接目录会被拒绝。文本替换只处理允许列表中的 UTF-8 文件；未知扩展名和非 UTF-8 内容原样保留。

推荐令牌：

- `{{ProjectCode}}`、`{{ProjectName}}`
- `{{MainProjectName}}`
- `{{RootNamespace}}`
- `{{Version}}`
- `{{BusinessType}}`
- `{{TargetFramework}}`

旧 `PCBA` 字面量继续兼容。新模板优先使用显式令牌。生成后任何未解析的 `{{Token}}`，以及未排除区域内残留的 `PCBA`，都会使自检失败。

## 新增业务模板

1. 创建 `Template/<BusinessName>/template.config.json`。
2. 设置 `schemaVersion: 1`、说明和 `businessType`；`businessType` 必须与目录名忽略大小写后相同。
3. 在业务目录中提供自己的前端业务逻辑、WPF UI、App 和完整解决方案；只放业务特有或需要覆盖的文件。
4. 需要移除公共文件时使用 `deleteFromOutput`，不要添加空文件占位。
5. 运行模板目录发现测试和该业务模板的生成测试。

无需修改 `InteractiveWizard` 或 CLI 的业务类型列表。

## 版本和依赖

模板版本基线位于 `Template/Common/Directory.Build.props`。默认输出版本为基线 patch 加一；指定发布版本时使用 `--version`。流水线只写生成项目，不写回模板。

NuGet 版本集中在 `Template/Common/Directory.Packages.props`。新增或升级包时只在中央文件声明版本，各 `.csproj` 保留无版本的 `PackageReference`。

Machine 的 WPF 项目目标框架为 `net8.0-windows10.0.19041.0`。Common 公共层保持可独立构建；DeviceLink 库有意保持 `net6.0` 与 `netstandard2.0` 目标，后续将作为 GitHub 子模块独立维护，不要将其升级到 .NET 8。

## 凭据与运行时默认值

- FTP 用户名和密码只从 `TESTRIG_FTP_USER`、`TESTRIG_FTP_PASSWORD` 读取。
- 登录历史只允许持久化用户名，不保存或恢复密码。
- 外部 API 地址默认留空；未配置时客户端应短路，不访问占位地址。
- 真实硬件默认关闭，必须由部署环境显式启用。

## 验证清单

```powershell
dotnet test .\WPFCli.Tests\WPFCli.Tests.csproj --nologo
dotnet build .\WPFCli\WPFCli.csproj --nologo
dotnet build .\Template\Common\PCBA.sln --nologo -m:1
dotnet run --project .\WPFCli\WPFCli.csproj -- --biz machine --code VerifyMachine --output $env:TEMP\VerifyMachine
dotnet build .\Template\Common\src\libs\DeviceLink\DeviceLink.sln --nologo -m:1
dotnet pack .\WPFCli\WPFCli.csproj -c Release -o .\artifacts --nologo
```

打包相关修改还必须把 `TestRigCli` 安装到隔离工具目录，并在仓库外运行 `--list-templates` 和一次 `--dry-run --no-build`。提交前运行 `git diff --check`，确认未加入 `bin`、`obj`、临时 staging、备份或生成输出。
