# 模板清理与构建流程增强 — 实施计划

## Context（背景与动机）

WPFCli 工具的重构与模板编译修复均已完成，但模板本身仍存在三类问题：

1. **PS02 设备代码污染**：模板中嵌入了 PS02 变送器专用代码（驱动、处理器、工装清单），使模板从"通用工装模板"退化为"PS02 专用模板"。每次为新设备生成项目时，PS02 残留代码会误导开发者。

2. **混淆/打包与编译脱节**：当前 WPFCli 流程是"编译→生成 PS1→交互式提示执行"，用户需要手动确认执行 PS1。用户期望"dotnet build 成功后自动执行 PS1"。

3. **版本号写入 bug**：`VersionManager.WriteVersion` 只替换现有 `<Version>` 标签，但模板 csproj 中没有该标签，导致版本号 1.0.1 根本没写入生成的项目。同时版本号分散在各 csproj 中，难以统一管理。

本计划通过三项改造解决上述问题：移除 PS02 设备代码、集成 MSBuild Target 自动执行 PS1、在 Directory.Build.props 中统一版本号。

---

## 当前状态分析

### 1. PS02 代码分布（需移除）

**可直接删除的文件/目录**：
- [src/03.Devices/PCBA.Devices/Dut/PS02/](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices/Dut/PS02/) — PS02Dut.cs, SimulatedPS02Dut.cs
- [src/03.Devices/PCBA.Devices.Abstractions/IPS02Dut.cs](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices.Abstractions/IPS02Dut.cs)
- [src/04.TestSteps/PCBA.TestSteps/PS02/](file:///e:/WPFCli/Template/src/04.TestSteps/PCBA.TestSteps/PS02/) — PS02Handlers.cs
- [src/05.Jigs/PCBA.Jigs/Manifests/PS02/](file:///e:/WPFCli/Template/src/05.Jigs/PCBA.Jigs/Manifests/PS02/) — PS02_Board.json
- [src/libs/DeviceLink/devices/DeviceLink.Device.PS02/](file:///e:/WPFCli/Template/src/libs/DeviceLink/devices/DeviceLink.Device.PS02/) — 整个项目目录

**需修改的文件**：
- [PCBA.Devices.csproj:21](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices/PCBA.Devices.csproj#L21) — 移除 DeviceLink.Device.PS02 的 ProjectReference
- [MainViewModel.cs:99-117](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/MainViewModel.cs#L99-L117) — 删除 PS02_Board 自动加载逻辑
- [ManifestMaintenanceViewModel.cs:53-58](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/ManifestMaintenanceViewModel.cs#L53-L58) — 删除 PS02_Board 自动加载逻辑
- [ResultDbContext.cs:80,82,144](file:///e:/WPFCli/Template/src/02.Infrastructure/PCBA.Infrastructure/Data/ResultDbContext.cs#L80) — 表名 `ps02_test_data` → `test_data`，注释通用化
- [template.config.json:10](file:///e:/WPFCli/Template/template.config.json#L10) — 移除 `reservedNames: ["PS02"]`
- 注释清理（Manifest.cs, ConnectionSettings.cs, IStandardBox.cs, IDutDevice.cs, ConnectionConfigViewModel.cs）— PS02 改为通用描述

### 2. 编译安全性验证

- `DutDriverRegistry.AutoRegisterFromAssembly`（[DutDriverRegistry.cs:50](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices/Dut/DutDriverRegistry.cs#L50)）使用反射自动发现 `[DutDriver]` 特性类。删除 PS02Dut/SimulatedPS02Dut 后注册表为空，**不会编译失败**，未注册型号回落到通用 `SimulatedDut`。
- `TestStepsServiceCollectionExtensions.AddPcbaTestSteps`（[TestStepsServiceCollectionExtensions.cs:18](file:///e:/WPFCli/Template/src/04.TestSteps/PCBA.TestSteps/TestStepsServiceCollectionExtensions.cs#L18)）使用反射自动注册 IStepHandler。删除 PS02Handlers 后处理器列表为空，**不会编译失败**。
- `JigCatalog`（[JigCatalog.cs:80](file:///e:/WPFCli/Template/src/05.Jigs/PCBA.Jigs/JigCatalog.cs#L80)）扫描 Manifests 目录。删除 PS02_Board.json 后目录为空，只记录警告，**不会编译失败**。

### 3. 当前混淆/打包流程

- WPFCli 生成 `obfuscate.ps1` 和 `package.ps1` 到 `Output\<代码>\` 根目录
- PS1 中硬编码混淆目标列表（来自 template.config.json 的 `obfuscationTargets`）
- 编译后才生成 PS1，然后交互式提示用户执行
- 用户期望：dotnet build 成功后自动执行 PS1

### 4. 版本号 bug

- `VersionManager.WriteVersion`（[VersionManager.cs:94-144](file:///e:/WPFCli/WPFCli/Engine/VersionManager.cs#L94-L144)）只替换现有的 `<Version>` 标签
- 模板 csproj 中没有 `<Version>` 标签 → 版本号未写入
- `DetectVersion` 从主项目 csproj 读取 → 返回 null → 默认 "1.0.0"

---

## 拟定修改

### 改造 1：移除 PS02 设备代码

#### 1.1 删除文件/目录

| 路径 | 说明 |
|------|------|
| `Template/src/03.Devices/PCBA.Devices/Dut/PS02/` | PS02 真机驱动 + 仿真桩 |
| `Template/src/03.Devices/PCBA.Devices.Abstractions/IPS02Dut.cs` | PS02 专用接口 |
| `Template/src/04.TestSteps/PCBA.TestSteps/PS02/` | PS02 测试步骤处理器 |
| `Template/src/05.Jigs/PCBA.Jigs/Manifests/PS02/` | PS02 工装清单 |
| `Template/src/libs/DeviceLink/devices/DeviceLink.Device.PS02/` | DeviceLink PS02 通讯库 |

#### 1.2 修改 PCBA.Devices.csproj

移除第 21 行的 ProjectReference：
```xml
<!-- 删除此行 -->
<ProjectReference Include="../../libs/DeviceLink/devices/DeviceLink.Device.PS02/DeviceLink.Device.PS02.csproj" />
```

#### 1.3 修改 MainViewModel.cs

删除第 99-117 行的 PS02_Board 自动加载逻辑块：
```csharp
// 删除整个 try-catch 块
// PS02 单板模式：直接加载 PS02 板子，无需通过设备列表手动选择
try {
    var ps02 = catalog.Find("PS02_Board");
    ...
}
```

#### 1.4 修改 ManifestMaintenanceViewModel.cs

删除第 53-58 行的 PS02_Board 自动加载逻辑：
```csharp
// 删除此块
var ps02 = _catalog.Find("PS02_Board");
if (ps02 != null) { Current = ManifestEditModel.From(ps02); }
```

#### 1.5 修改 ResultDbContext.cs

- 第 80 行注释：`PS02 测试数据记录` → `测试数据记录`
- 第 82 行：`[Table("ps02_test_data")]` → `[Table("test_data")]`
- 第 144 行：`[Table("ps02_test_data_details")]` → `[Table("test_data_details")]`

#### 1.6 修改 template.config.json

移除 `reservedNames` 字段（PS02 代码已全部删除，无需保留）：
```json
{
  "placeholder": "PCBA",
  ...
  "obfuscationTargets": [ "Infrastructure", "Devices", "TestSteps", "Core.Engine" ]
  // 删除 reservedNames 行
}
```

#### 1.7 注释清理（可选但推荐）

将以下文件中提及 PS02 的注释改为通用描述：
- `Manifest.cs:10,15` — `PS02_Board` 示例 → `<设备>_Board` 示例
- `ConnectionSettings.cs:51` — `PS02 共享设备` → `共享设备`
- `IStandardBox.cs:6` — `PS02 测试平台` → `测试平台`
- `IDutDevice.cs:6` — `如 PS02` → `如 <设备型号>`
- `ConnectionConfigViewModel.cs:14,185,218` — `PS02 被检设备` → `被检设备`

### 改造 2：MSBuild Target 自动执行 PS1

#### 2.1 修改 PCBA.App.csproj

在 `</Project>` 前添加两个 Target：

```xml
<!-- ===== 编译后自动执行混淆和打包脚本（由 WPFCli 生成 PS1 后自动触发）===== -->
<!-- 条件：仅 Release 配置 + PS1 文件存在时才执行 -->
<Target Name="RunObfuscateScript" AfterTargets="Build"
        Condition="'$(Configuration)' == 'Release' And Exists('$(MSBuildProjectDirectory)\..\..\..\obfuscate.ps1')">
  <Message Text="====== 执行混淆脚本 ======" Importance="high" />
  <Exec Command="powershell.exe -NoProfile -ExecutionPolicy Bypass -File &quot;$(MSBuildProjectDirectory)\..\..\..\obfuscate.ps1&quot;" />
</Target>

<Target Name="RunPackageScript" AfterTargets="RunObfuscateScript"
        Condition="'$(Configuration)' == 'Release' And Exists('$(MSBuildProjectDirectory)\..\..\..\package.ps1')">
  <Message Text="====== 执行打包脚本 ======" Importance="high" />
  <Exec Command="powershell.exe -NoProfile -ExecutionPolicy Bypass -File &quot;$(MSBuildProjectDirectory)\..\..\..\package.ps1&quot;" />
</Target>
```

**路径说明**：`$(MSBuildProjectDirectory)` = `Output\<代码>\src\08.App\<代码>.App\`，向上 3 层回到 `Output\<代码>\`，即 PS1 所在位置。

**Condition 逻辑**：
- WPFCli 用户选了混淆 → 生成 obfuscate.ps1 → Target 执行
- WPFCli 用户没选混淆 → 不生成 obfuscate.ps1 → Target 跳过
- 模板自身编译（dotnet build PCBA.sln）→ PS1 不存在 → Target 跳过

#### 2.2 调整 WPFCli 流程

修改 [Program.cs](file:///e:/WPFCli/WPFCli/Program.cs)，将 PS1 生成移到编译前：

**当前流程**：
1. 拷贝模板 + 替换
2. 写入版本号
3. 编译项目
4. 生成混淆脚本（可选）
5. 生成打包脚本（可选）
6. 交互式执行 PS1
7. 产物清单

**新流程**：
1. 拷贝模板 + 替换
2. 写入版本号
3. **生成混淆脚本（可选）** ← 提前
4. **生成打包脚本（可选）** ← 提前
5. 编译项目（编译成功后 MSBuild Target 自动执行 PS1）
6. 产物清单

**删除的代码**：
- 删除步骤 6（交互式执行 PS1 的逻辑，约 Program.cs 第 135-184 行）
- 保留编译失败提示

**编译失败处理**：
- 如果 PS1 执行失败，`<Exec>` 会返回非零退出码，导致 dotnet build 失败
- WPFCli 捕获编译失败，提示用户查看日志
- 这符合"产物不完整则构建失败"的语义

### 改造 3：Directory.Build.props 统一版本

#### 3.1 修改 Directory.Build.props

添加版本号声明：

```xml
<Project>
  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <!-- ===== 统一版本号（所有 PCBA.* 项目共享）===== -->
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>
  </PropertyGroup>
</Project>
```

**为什么用 Directory.Build.props**：
- MSBuild 自动向上查找，所有子项目继承同一版本号
- WPFCli 修改此文件一处，即可同步全部项目版本
- 不含 "PCBA" 字符串，模板替换后仍生效

#### 3.2 修复 VersionManager.cs

**修改 `DetectVersion`**：从 `Directory.Build.props` 读取版本号（而非主项目 csproj）：

```csharp
public static string? DetectVersion(string templatePath, string mainProjectName)
{
    // 优先从 Directory.Build.props 读取（统一版本管理）
    var propsFile = Path.Combine(templatePath, "Directory.Build.props");
    if (File.Exists(propsFile))
    {
        var version = ReadVersionFromProps(propsFile);
        if (version != null) return version;
    }
    // 兜底：从主项目 csproj 读取（向后兼容）
    var csproj = Path.Combine(templatePath, "src", "08.App", mainProjectName, $"{mainProjectName}.csproj");
    return ReadVersionFromCsproj(csproj);
}
```

**修改 `WriteVersion`**：改为修改 `Directory.Build.props`（而非遍历所有 csproj）：

```csharp
public static void WriteVersion(string outputDir, string newVersion)
{
    var propsFile = Path.Combine(outputDir, "Directory.Build.props");
    if (!File.Exists(propsFile)) return;

    var content = File.ReadAllText(propsFile);
    var updated = false;

    // 替换或添加 <Version> 标签
    if (Regex.IsMatch(content, @"<Version>\s*[^<]*\s*</Version>", RegexOptions.IgnoreCase))
    {
        content = Regex.Replace(content,
            @"<Version>\s*[^<]*\s*</Version>",
            $"<Version>{newVersion}</Version>",
            RegexOptions.IgnoreCase);
        updated = true;
    }

    // 同样处理 <AssemblyVersion> 和 <FileVersion>
    // ...（使用 newVersion + ".0" 或保持一致）

    if (updated) File.WriteAllText(propsFile, content);
}
```

**好处**：
- 版本号集中在一处，修改 `Directory.Build.props` 即可
- 不再遍历所有 csproj，避免误改 DeviceLink 等子项目
- 修复了"模板无 Version 标签导致不写入"的 bug

#### 3.3 WPFCli 自身版本管理（可选）

[WPFCli.csproj](file:///e:/WPFCli/WPFCli/WPFCli.csproj) 的 `<Version>2.0.0</Version>` 保持不变（工具自身版本与模板版本独立）。

---

## 假设与决策

1. **PS02 移除后模板可编译**：基于反射自动注册机制（DutDriverRegistry、TestStepsServiceCollectionExtensions），删除 PS02 代码后模板仍能编译。通用 `SimulatedDut` 作为回落驱动。
2. **DeviceLink.Device.PS02 一并删除**：PCBA.Devices.csproj 移除引用后，该通讯库不再被需要。
3. **MSBuild Target 用 Condition 控制**：PS1 不存在时 Target 跳过，不影响模板自身编译。
4. **版本号统一到 Directory.Build.props**：不再在每个 csproj 中声明 Version，一处修改全局生效。
5. **不生成 CHANGELOG.md**：用户未选择此项，暂不实施。但 Directory.Build.props 中的版本号递增本身具有审计意义（每次构建 patch+1）。
6. **WPFCli 流程调整**：PS1 生成提前到编译前，使编译时 Target 能找到 PS1。

---

## 实施步骤

| 步骤 | 动作 | 验证 |
|------|------|------|
| 1 | 删除 PS02 相关文件/目录（5 个路径） | 文件不存在 |
| 2 | 修改 PCBA.Devices.csproj 移除 DeviceLink.Device.PS02 引用 | csproj 无 PS02 引用 |
| 3 | 修改 MainViewModel.cs 删除 PS02_Board 自动加载 | 编译通过 |
| 4 | 修改 ManifestMaintenanceViewModel.cs 删除 PS02_Board 自动加载 | 编译通过 |
| 5 | 修改 ResultDbContext.cs 表名通用化 | 编译通过 |
| 6 | 修改 template.config.json 移除 reservedNames | JSON 合法 |
| 7 | 清理注释中的 PS02 描述（5 个文件） | 编译通过 |
| 8 | `dotnet build PCBA.sln -c Release` 验证模板编译 | 0 errors |
| 9 | 修改 PCBA.App.csproj 添加 MSBuild Target | csproj 语法正确 |
| 10 | 修改 Directory.Build.props 添加版本号声明 | 编译通过 |
| 11 | 修改 VersionManager.cs 修复 DetectVersion 和 WriteVersion | WPFCli 编译通过 |
| 12 | 修改 Program.cs 调整流程（PS1 生成提前到编译前） | WPFCli 编译通过 |
| 13 | `dotnet build WPFCli.csproj` 验证 WPFCli 编译 | 0 errors |
| 14 | 端到端验证：运行 WPFCli 生成 PT01 | 全流程通过 |

---

## 验证步骤

### 1. 模板编译验证

```powershell
cd e:\WPFCli\Template
dotnet build PCBA.sln -c Release
```
预期：`Build succeeded. 0 Error(s)`，PS02 相关警告消失。

### 2. WPFCli 编译验证

```powershell
cd e:\WPFCli\WPFCli
dotnet build -c Release
```
预期：`0 errors, 0 warnings`。

### 3. 端到端验证

```powershell
# 清理旧产物
Remove-Item -Recurse -Force e:\WPFCli\Output\PT01

# 运行 WPFCli（输入 PT01，选择混淆=否，打包=否）
echo "PT01`nn`nn`ny" | dotnet run --project e:\WPFCli\WPFCli\WPFCli.csproj -c Release
```
预期：
- 步骤 1：拷贝模板 + 替换 PCBA→PT01
- 步骤 2：写入版本号 1.0.0 → 1.0.1 到 `Directory.Build.props`
- 步骤 3-4：生成 PS1（如果选择了混淆/打包）
- 步骤 5：编译成功，MSBuild Target 检查 PS1 是否存在
- 产物清单显示 PT01.App.exe

### 4. 版本号写入验证

```powershell
# 检查生成的 Directory.Build.props 是否包含新版本号
Get-Content e:\WPFCli\Output\PT01\Directory.Build.props | Select-String "Version"
```
预期：`<Version>1.0.1</Version>`

### 5. MSBuild Target 验证

```powershell
# 选择混淆=是，打包=是，重新生成
echo "PT02`ny`ny`ny" | dotnet run --project e:\WPFCli\WPFCli\WPFCli.csproj -c Release
# 检查编译日志是否包含 "执行混淆脚本" 和 "执行打包脚本"
```
预期：编译日志显示 Target 执行，混淆后的 DLL 覆盖到 publish 目录，安装包生成。

---

## 风险与回退

- **风险**：删除 PS02 代码后可能有遗漏的引用导致编译失败。
  - **缓解**：编译验证步骤会立即发现问题，逐个修复。
- **风险**：MSBuild Target 执行 PS1 失败会导致 dotnet build 失败。
  - **缓解**：用户可通过不生成 PS1（选择混淆=否、打包=否）来跳过 Target。
- **风险**：VersionManager 修改后可能影响已有 Output 产物。
  - **缓解**：每次生成前清理 Output 目录。
- **回退**：所有修改均为增量修改，可通过 git revert 恢复。
