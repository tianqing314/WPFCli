# 修复模板项目编译错误 — 实施计划

## 摘要

WPFCli 工具的重构已在前一次会话中完成并验证通过，但模板项目（`e:\WPFCli\Template\PCBA.sln`）本身存在编译错误：10 个 `PCBA.*` 项目的 `.csproj` 文件均未启用 `<ImplicitUsings>enable</ImplicitUsings>` 和 `<Nullable>enable</Nullable>`，但源代码大量使用 `IAsyncDisposable`、`Task<>`、`CancellationToken`、`IReadOnlyList<>`、`string?` 等需要这些设置才能识别的类型与语法。

本计划通过在模板根目录创建一个 `Directory.Build.props` 文件，统一为所有项目启用 ImplicitUsings / Nullable / LangVersion，使模板项目能正常编译，从而让 WPFCli 工具的"拷贝→替换→编译→混淆→打包"完整流程端到端跑通。

## 当前状态分析

### 1. WPFCli 工具本身（已完成，无需修改）
- `e:\WPFCli\WPFCli\` 下 10 个 Engine 文件 + 2 个 Models 文件 + Program.cs
- 构建通过：0 errors, 0 warnings
- 流程：向导收集选项 → 拷贝模板 → PCBA→代号替换 → dotnet build → 生成 .ps1 脚本

### 2. 模板项目编译错误根因
通过 `Select-String` 扫描确认，10 个 PCBA.* 项目的 .csproj 文件均缺失关键属性：

| 项目 | TargetFramework | ImplicitUsings | Nullable |
|------|-----------------|---------------|----------|
| PCBA.Core.Abstractions | net8.0 | ❌ | ❌ |
| PCBA.Core.Engine | net8.0 | ❌ | ❌ |
| PCBA.Infrastructure | net8.0 | ❌ | ❌ |
| PCBA.Devices.Abstractions | net8.0 | ❌ | ❌ |
| PCBA.Devices | net8.0 | ❌ | ❌ |
| PCBA.Devices.BleWin | net8.0-windows10.0.19041.0 | ❌ | ✅ enable |
| PCBA.TestSteps | net8.0 | ❌ | ❌ |
| PCBA.Jigs | net8.0 | ❌ | ❌ |
| PCBA.UI.Shared | net8.0-windows | ❌ | ❌ |
| PCBA.App | net8.0-windows10.0.19041.0 | ❌ | ❌ |

而源码中使用的关键类型（均需对应 using）：
- `IAsyncDisposable`（[Devices.cs:6](file:///e:/WPFCli/Template/src/01.Core/PCBA.Core.Abstractions/Devices.cs#L6)）→ 需 `using System;`
- `Task<>` / `CancellationToken`（[IStandardBox.cs:18](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices.Abstractions/IStandardBox.cs#L18)）→ 需 `using System.Threading.Tasks;`
- `IReadOnlyList<>` / `FirstOrDefault`（[Steps.cs:100-114](file:///e:/WPFCli/Template/src/01.Core/PCBA.Core.Abstractions/Steps.cs#L100-L114)）→ 需 `System.Collections.Generic` / `System.Linq`
- `string?` 可空标注 → 需 `<Nullable>enable</Nullable>`

至少 20+ 个源文件受影响。

### 3. DeviceLink 子项目
- 位于 `src/libs/DeviceLink/`，被 `template.config.json` 的 `excludeFromReplacement` 排除（不做 PCBA→代号替换）
- 有独立的 `DeviceLink.sln`，目标框架为 `netstandard2.0;net6.0`
- 所有项目已显式设置 `<Nullable>`（enable 或 disable），1 个已设置 `<ImplicitUsings>enable</ImplicitUsings>`
- **PCBA.Devices 项目引用了 4 个 DeviceLink.Device.* 项目**（[PCBA.Devices.csproj:21-24](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices/PCBA.Devices.csproj#L21-L24)），所以 DeviceLink 也必须能编译

### 4. 已无 Directory.Build.props
模板根目录下无 `Directory.Build.props`、`global.json`、`nuget.config`，各项目独立配置。

## 拟定修改

### 修改 1：创建 `Directory.Build.props`（核心修复）

**文件**：`e:\WPFCli\Template\Directory.Build.props`（新建）

**内容**：
```xml
<Project>
  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

**为什么用 Directory.Build.props 而非逐个改 csproj**：
1. MSBuild 自动向上查找 `Directory.Build.props`，对模板下所有项目生效
2. 单点配置，避免修改 10 个 csproj 文件
3. 该文件不含 "PCBA" 字样，模板替换时不被改动，复制到 Output 后仍生效
4. 项目级 csproj 的显式设置（如 DeviceLink 的 `<Nullable>disable</Nullable>`）会覆盖此文件，互不冲突

**`ImplicitUsings=enable` 自动注入的 using**（解决所有已知缺失）：
- `System`（IAsyncDisposable, Console, Environment）
- `System.Collections.Generic`（List<>, Dictionary<>, IEnumerable<>, IReadOnlyList<>）
- `System.IO`（Path, File, Directory, Stream）
- `System.Linq`（FirstOrDefault, Select, Where, Any）
- `System.Net.Http`（HttpClient）
- `System.Threading`（CancellationToken）
- `System.Threading.Tasks`（Task, Task<>）
- `System.Collections.Generic`（重复列出为完整性）

### 修改 2：清理 Output 旧产物（可选）

**目录**：`e:\WPFCli\Output\PT01\`

前次验证测试生成的 PT01 产物已无用途。若存在则删除，避免干扰新一轮测试。

### 修改 3：验证编译

在 `e:\WPFCli\Template\` 下执行：
```powershell
dotnet build PCBA.sln -c Release
```

预期：0 errors。若仍有少数源码级错误（如个别文件使用了 ImplicitUsings 不覆盖的命名空间），逐个补充 `using` 指令。

### 修改 4：端到端验证（可选但推荐）

运行 WPFCli 工具，使用示例代号（如 `PT01`）走完整个流程：
1. 拷贝模板 → 替换 PCBA→PT01
2. dotnet build 编译生成的 PT01.sln
3. 生成 obfuscate.ps1 / package.ps1
4. 验证产物清单

## 假设与决策

1. **假设**：ImplicitUsings 能解决绝大多数编译错误。若有少数不覆盖的命名空间（如 `System.Text.Json`、`System.Diagnostics`），逐文件补 `using`。
2. **决策**：不修改 DeviceLink 项目的 csproj。它们已各自配置，且 csproj 级设置会覆盖 Directory.Build.props。若 DeviceLink 编译失败，单独处理。
3. **决策**：不修改 WPFCli 工具本身。前次重构已验证完成，本次只修复模板项目。
4. **假设**：用户希望模板能直接编译通过，以便 WPFCli 流程端到端跑通。

## 实施步骤

| 步骤 | 动作 | 验证 |
|------|------|------|
| 1 | 创建 `e:\WPFCli\Template\Directory.Build.props` | 文件存在，内容正确 |
| 2 | 在 `e:\WPFCli\Template\` 下执行 `dotnet build PCBA.sln -c Release` | 0 errors |
| 3 | 若有残余错误，逐文件补充 `using` 指令 | 0 errors |
| 4 | （可选）删除 `e:\WPFCli\Output\PT01\` 旧产物 | 目录不存在 |
| 5 | （可选）运行 WPFCli 工具端到端验证 | 生成 PT01 项目可编译 |

## 验证步骤

1. **编译验证**：
   ```powershell
   cd e:\WPFCli\Template
   dotnet build PCBA.sln -c Release
   ```
   预期输出：`Build succeeded. 0 Error(s)`

2. **Directory.Build.props 生效验证**：
   ```powershell
   dotnet build PCBA.sln -c Release -v:detail | Select-String "ImplicitUsings"
   ```
   确认属性被正确读取。

3. **WPFCli 端到端验证**（可选）：
   ```powershell
   cd e:\WPFCli\WPFCli
   dotnet run
   ```
   按向导输入代号 `PT01`，确认编译步骤通过。

## 风险与回退

- **风险**：`<Nullable>enable</Nullable>` 可能在某些 PCBA.* 项目中引入新的 nullable 警告（非错误）。这些警告不影响编译。
- **风险**：DeviceLink 项目可能因 Directory.Build.props 引入新的行为。但因其 csproj 已显式设置 Nullable，不受影响。
- **回退**：删除 `Directory.Build.props` 即可恢复原状。
