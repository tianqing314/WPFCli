# WPFCli 改造为模板驱动 + 混淆/打包的 CLI 工具

## Context（背景）

当前 WPFCli 是一个 **WPF 项目脚手架**，通过 `__KEY__` 占位符 + 8 个硬编码 Generator 类生成项目骨架，模板代码以 C# 字符串字面量形式散落在 `Generators/` 目录中。

新需求要求：
1. 模板以**完整源码项目形式**存放在 `Template\` 目录（用户已放置好）
2. 用户输入**项目代号**（如 PT01），对模板做精确替换（PCBA → PT01）生成新项目
3. 编译成功后可选执行 **Obfuscar 混淆**（业务层 4 个 DLL）
4. 编译成功后可选生成 **Inno Setup 安装包**
5. 混淆/打包逻辑生成独立 `obfuscate.ps1` + `package.ps1`，编译后**交互式确认**执行

参考 `E:\ExeBuilder` 项目中的成熟实现，但用 C# 在 WPFCli 内重新实现（不直接拷贝）。

最终产物：用户运行 `wpf-cli` → 输入代号 PT01 → 自动生成、编译、混淆、打包，得到 `installer\PT01-Setup-1.0.1.exe`。

---

## 关键设计决策

| 维度 | 决策 |
|---|---|
| 模板代号 | `PCBA`（在 `template.config.json` 中声明） |
| 用户输入 | 项目代号（如 PT01），校验不能与模板中已有标识符冲突（PS02 等保留名） |
| 输出路径 | **固定** `Output\<代号>`，不询问 |
| 替换方式 | **精确替换**（`\bPCBA\b` 全词匹配），原样不转大小写 |
| 替换范围 | 文件名/文件夹名、C# 命名空间、配置键 Pcba、SQL/小写 pcba |
| 排除目录 | DeviceLink（src/libs/DeviceLink）、tools、docs、.reasonix（不替换内容） |
| 拷贝排除 | bin/obj/.reasonix/.vs/.git（编译产物，不拷贝） |
| GUID | 保留模板原始 GUID（已确认模板唯一） |
| 编译 | `dotnet build <sln> -c Release`（net8.0-windows10.0.19041.0） |
| 混淆目标 | Infrastructure + Devices + TestSteps + Core.Engine 共 4 个 DLL |
| Obfuscar 策略 | WPF 安全混淆（AnalyzeXaml=true, KeepPublicApi=true, RenameProperties=false） |
| PS 脚本 | 两个独立：`obfuscate.ps1` + `package.ps1`，相对路径 + `$ProjectRoot` |
| 执行时机 | 编译成功后**交互式确认**是否执行 |
| 版本号 | 自动递增 patch（1.0.0 → 1.0.1），从 csproj 读取 |
| ExeBuilder 复用 | 参考**逻辑**，C# 在 WPFCli 中重新实现 |

---

## 实施步骤

### 阶段 1: 模型与配置

#### 1.1 创建 `Template/template.config.json`
新增模板元数据，声明占位符、排除规则、混淆目标：

```json
{
  "placeholder": "PCBA",
  "description": "PCBA 测试工装模板",
  "targetFramework": "net8.0-windows10.0.19041.0",
  "configuration": "Release",
  "mainProjectName": "PCBA.App",
  "excludeFromCopy": ["bin", "obj", ".reasonix", ".vs", ".git"],
  "excludeFromReplacement": ["src/libs/DeviceLink", "tools", "docs"],
  "obfuscationTargets": ["Infrastructure", "Devices", "TestSteps", "Core.Engine"],
  "reservedNames": ["PS02"]
}
```

#### 1.2 创建 `WPFCli/Models/TemplateConfig.cs`
对应上述 JSON 的 C# 类（用 System.Text.Json 反序列化）。

#### 1.3 创建 `WPFCli/Models/BuildOptions.cs`
```csharp
public class BuildOptions
{
    public string ProjectCode { get; set; } = "";        // 用户输入的代号（如 PT01）
    public string OutputDir { get; set; } = "";          // 固定 Output\<代号>
    public string TemplatePath { get; set; } = "";       // Template\ 绝对路径
    public TemplateConfig Template { get; set; } = new();
    public bool EnableObfuscation { get; set; }
    public bool EnablePackaging { get; set; }
    public string Version { get; set; } = "1.0.0";       // 自动递增后
}
```

### 阶段 2: 核心引擎

#### 2.1 重写 `WPFCli/Engine/InteractiveWizard.cs`
保留现有 UI 风格（Banner、PromptYesNo、配置摘要表格），简化为 3 步：
- [1/3] 项目代号（必填，校验合法性、保留名冲突）
- [2/3] 是否混淆？[y/N]
- [3/3] 是否打包？[Y/n]
- 配置摘要 + 确认

代号合法性校验规则：
- 长度 2-20 字符
- 仅字母数字（首字符必须字母）
- 不在 `TemplateConfig.ReservedNames` 列表中（如 PS02）

#### 2.2 创建 `WPFCli/Engine/TemplateBuilder.cs`
负责拷贝 + 精确替换，对外暴露 `Build(options)` 方法：

```csharp
public static class TemplateBuilder
{
    public static void Build(BuildOptions opts, Action<string> onProgress)
    {
        // 1. 拷贝 Template\ → Output\<代号>\
        CopyDirectory(opts.TemplatePath, opts.OutputDir, opts.Template.ExcludeFromCopy);
        // 2. 替换文件内容（排除指定目录）
        ReplaceContentInFiles(opts.OutputDir, opts.Template, opts.ProjectCode);
        // 3. 重命名文件/文件夹（先深后浅）
        RenameFilesAndDirs(opts.OutputDir, opts.Template, opts.ProjectCode);
    }
}
```

替换实现要点：
- **内容替换**用 `Regex.Replace(content, @"\bPCBA\b", "PT01")`，原样不转大小写
- **文件名替换**用同样正则
- **排除目录**检查：路径包含 `src/libs/DeviceLink` / `tools` / `docs` 则跳过内容替换（但仍拷贝）
- 二进制文件（.exe/.dll/.png/.ico/.xlsx）跳过内容替换，只做文件名替换

#### 2.3 创建 `WPFCli/Engine/ProjectCompiler.cs`
负责调用 `dotnet build`：

```csharp
public static class ProjectCompiler
{
    public static async Task<bool> CompileAsync(string slnPath, string config, Action<string> onOutput)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"build \"{slnPath}\" -c {config} --nologo",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        // 实时推送输出
        // 退出码 0 视为成功
    }

    public static string GetPublishDir(BuildOptions opts)
        => Path.Combine(opts.OutputDir,
            "src", "08.App", $"{opts.ProjectCode}.App",
            "bin", opts.Template.Configuration,
            opts.Template.TargetFramework);
}
```

#### 2.4 创建 `WPFCli/Engine/VersionManager.cs`
参考 `E:\ExeBuilder\Config\VersionManager.cs` 实现（轻量化）：
- `DetectVersion(projectDir)`：从主项目 .csproj 读取 `<Version>` 标签
- `IncrementPatch(version)`：1.0.0 → 1.0.1（递增第 3 段，与 ExeBuilder 不同——用户要求 patch 而非 build）
- 生成后将新版本写回 csproj 的 `<Version>` 和 `<AssemblyVersion>`

### 阶段 3: PowerShell 脚本生成

#### 3.1 创建 `WPFCli/Engine/PowerShellScriptBuilder.cs`
通用 .ps1 生成器，提供：
- `BuildObfuscatePs1(opts, obfuscarPath)` → 返回 .ps1 内容字符串
- `BuildPackagePs1(opts, isccPath)` → 返回 .ps1 内容字符串

#### 3.2 创建 `WPFCli/Engine/Obfuscator.cs`
参考 `E:\ExeBuilder\Services\BuildService.cs:497-566` 的 `BuildObfuscarXml` 方法，重新实现：

```csharp
public static class Obfuscator
{
    // 5 级检测：PATH → dotnet tool list → NuGet packages → where → fallback
    public static string? FindObfuscar();

    // 生成 Obfuscar XML 内容（WPF 安全策略）
    // 包含：AnalyzeXaml=true, KeepPublicApi=true, HidePrivateApi=true,
    //       RenameProperties=false, RenameEvents=false, HideStrings=true,
    //       UseUnicodeNames=true, AssemblySearchPath 自动添加 .NET 框架路径
    public static string BuildObfuscarXml(List<string> modules, string outPath, string publishDir);

    // 生成 obfuscate.ps1 内容
    public static string GenerateObfuscatePs1(BuildOptions opts, string obfuscarPath);
}
```

`obfuscate.ps1` 模板（基于 `$ProjectRoot` 相对路径）：
```powershell
# ===== Obfuscar 混淆脚本（由 WPFCli 自动生成）=====
# 项目代号: PT01  版本: 1.0.1  生成时间: 2026-07-18 12:00:00
$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $ProjectRoot "src\08.App\PT01.App\bin\Release\net8.0-windows10.0.19041.0"
$ObfOutDir = Join-Path $ProjectRoot "build\obfuscated"

Write-Host "====== 开始混淆 ======" -ForegroundColor Cyan

# 1. 检测 Obfuscar
$obfuscar = Find-Obfuscar
if (-not $obfuscar) { Write-Host "未找到 Obfuscar" -ForegroundColor Red; exit 1 }

# 2. 生成 obfuscar.xml
$xml = @"
<?xml version="1.0"?>
<Obfuscator>
  ...
  <Module file="$(Join-Path $PublishDir 'PT01.Infrastructure.dll')" />
  ...
</Obfuscator>
"@
$xmlPath = Join-Path $ProjectRoot "build\obfuscar.xml"
$xml | Out-File $xmlPath -Encoding UTF8

# 3. 执行混淆
Push-Location $PublishDir
& $obfuscar $xmlPath
$exitCode = $LASTEXITCODE
Pop-Location
if ($exitCode -ne 0) { exit 1 }

# 4. 覆盖回 publish 目录
Copy-Item "$ObfOutDir\*" $PublishDir -Force -Recurse
Write-Host "✓ 混淆完成" -ForegroundColor Green

function Find-Obfuscar { ... }
```

#### 3.3 创建 `WPFCli/Engine/InstallerPackager.cs`
参考 `E:\ExeBuilder\Services\BuildService.cs:707-785` 的 `GenerateInnoScript` 方法：

```csharp
public static class InstallerPackager
{
    // 检测 ISCC.exe：注册表 + Program Files + PATH
    public static string? FindInnoSetup();

    // 生成 .iss 脚本内容（动态收集 publish 目录文件清单）
    public static string GenerateInnoScript(BuildOptions opts, List<string> files);

    // 生成 package.ps1 内容
    public static string GeneratePackagePs1(BuildOptions opts, string isccPath);
}
```

`package.ps1` 模板：
```powershell
# ===== Inno Setup 打包脚本（由 WPFCli 自动生成）=====
# 项目代号: PT01  版本: 1.0.1
$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$PublishDir = Join-Path $ProjectRoot "src\08.App\PT01.App\bin\Release\net8.0-windows10.0.19041.0"
$InstallerDir = Join-Path $ProjectRoot "installer"

Write-Host "====== 开始打包 ======" -ForegroundColor Cyan

$iscc = Find-InnoSetup
if (-not $iscc) { Write-Host "未找到 Inno Setup" -ForegroundColor Red; exit 1 }

# 生成 .iss
$iss = @"
[Setup]
AppName=PT01
AppVersion=1.0.1
AppPublisher=WPFCli
DefaultDirName={autopf}\PT01
DefaultGroupName=PT01
OutputBaseFilename=PT01-Setup-1.0.1
OutputDir=$InstallerDir
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest

[Files]
Source: "$PublishDir\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\PT01"; Filename: "{app}\PT01.App.exe"
Name: "{userdesktop}\PT01"; Filename: "{app}\PT01.App.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; Flags: unchecked

[Run]
Filename: "{app}\PT01.App.exe"; Description: "立即启动"; Flags: nowait postinstall skipifsilent
"@

$issPath = Join-Path $ProjectRoot "build\PT01.iss"
$iss | Out-File $issPath -Encoding UTF8

& $iscc $issPath
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "✓ 打包完成: $InstallerDir\PT01-Setup-1.0.1.exe" -ForegroundColor Green

function Find-InnoSetup { ... }
```

#### 3.4 创建 `WPFCli/Engine/PowerShellRunner.cs`
执行 .ps1 脚本的封装：
```csharp
public static async Task<int> RunScriptAsync(string scriptPath, Action<string> onOutput)
{
    var psi = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    // 实时推送输出
}
```

### 阶段 4: 主流程

#### 4.1 重写 `WPFCli/Program.cs`
完整流程：

```csharp
static async Task<int> Main(string[] args)
{
    // 1. 加载模板配置
    var templateConfig = LoadTemplateConfig();
    if (templateConfig == null) return 1;

    // 2. 交互式向导
    var opts = InteractiveWizard.Run(templateConfig);
    if (opts == null) return 0;

    // 3. 环境检测（dotnet 必需，Obfuscar/ISCC 按需检测并提示）
    EnvironmentChecker.Check(opts);

    // 4. 版本号管理
    opts.Version = VersionManager.IncrementPatch(
        VersionManager.DetectVersion(Path.Combine(opts.TemplatePath, ...)) ?? "1.0.0");

    // 5. 流水线
    RunStep(1, "拷贝模板并替换占位符", () => TemplateBuilder.Build(opts, ...));
    RunStep(2, "递增版本号", () => VersionManager.WriteVersion(opts));
    RunStep(3, "编译项目", () => ProjectCompiler.CompileAsync(opts).GetAwaiter().GetResult());
    // (可选) 生成脚本
    if (opts.EnableObfuscation) RunStep(4, "生成混淆脚本", () => Obfuscator.GenerateScript(opts));
    if (opts.EnablePackaging)    RunStep(5, "生成打包脚本", () => InstallerPackager.GenerateScript(opts));

    // 6. 交互式执行 .ps1
    if (opts.EnableObfuscation && PromptYesNo("是否立即执行混淆脚本？")) {
        await PowerShellRunner.RunScriptAsync(Path.Combine(opts.OutputDir, "obfuscate.ps1"), ...);
    }
    if (opts.EnablePackaging && PromptYesNo("是否立即执行打包脚本？")) {
        await PowerShellRunner.RunScriptAsync(Path.Combine(opts.OutputDir, "package.ps1"), ...);
    }

    // 7. 输出产物清单
    PrintResult(opts);
}
```

#### 4.2 创建 `WPFCli/Engine/EnvironmentChecker.cs`
检测 .NET SDK 必需、Obfuscar 可选、ISCC 可选，缺失时给出安装提示但不阻塞（编译步骤会自然失败）。

### 阶段 5: 清理无用代码

完成后删除：

| 路径 | 原因 |
|---|---|
| `WPFCli/Generators/` 整个目录（8 个 .cs） | 旧硬编码生成器，被模板替换 |
| `WPFCli/Models/ProjectOptions.cs` | 旧模型，被 BuildOptions 替代 |
| `WPFCli/Engine/TemplateEngine.cs` | 旧 `__KEY__` 占位符引擎，被 TemplateBuilder 替代 |
| `WPFCli/Output/ICS.TestDemo1/` | 旧生成示例 |
| `WPFCli/Output/ICS.TestDemo2/` | 旧生成示例 |
| `WPFCli/Output/ICS.TestVerify/` | 旧生成示例 |
| `WPFCli/Output/TestDemo2/` | 旧生成示例 |
| `WPFCli/Output/ICS.DynamicTest/` | 旧生成示例（已迁移为 Template） |

保留：
- `WPFCli/Engine/FileWriter.cs`（通用工具，仍可复用 `EnsureDirectory`/`CleanDirectory`）

---

## 关键文件清单

### 新增文件（11 个）

| 路径 | 作用 |
|---|---|
| `Template/template.config.json` | 模板元数据 |
| `WPFCli/Models/TemplateConfig.cs` | 模板配置模型 |
| `WPFCli/Models/BuildOptions.cs` | 构建选项模型 |
| `WPFCli/Engine/TemplateBuilder.cs` | 拷贝 + 精确替换 |
| `WPFCli/Engine/ProjectCompiler.cs` | dotnet build |
| `WPFCli/Engine/VersionManager.cs` | 版本号递增 |
| `WPFCli/Engine/Obfuscator.cs` | Obfuscar 检测 + XML 生成 + .ps1 生成 |
| `WPFCli/Engine/InstallerPackager.cs` | ISCC 检测 + .iss 生成 + .ps1 生成 |
| `WPFCli/Engine/PowerShellScriptBuilder.cs` | 通用 .ps1 内容构建 |
| `WPFCli/Engine/PowerShellRunner.cs` | 执行 .ps1 |
| `WPFCli/Engine/EnvironmentChecker.cs` | 环境检测 |

### 重写文件（2 个）

| 路径 | 改动 |
|---|---|
| `WPFCli/Program.cs` | 重写主流程，async Main，集成新流水线 |
| `WPFCli/Engine/InteractiveWizard.cs` | 简化为 3 步向导（代号 + 混淆 + 打包） |

### 删除文件

| 路径 |
|---|
| `WPFCli/Generators/BusinessProjectGenerator.cs` |
| `WPFCli/Generators/CommonProjectGenerator.cs` |
| `WPFCli/Generators/ModulesProjectGenerator.cs` |
| `WPFCli/Generators/PackagerGenerator.cs` |
| `WPFCli/Generators/ServiceProjectGenerator.cs` |
| `WPFCli/Generators/SolutionGenerator.cs` |
| `WPFCli/Generators/TestsProjectGenerator.cs` |
| `WPFCli/Generators/UiProjectGenerator.cs` |
| `WPFCli/Models/ProjectOptions.cs` |
| `WPFCli/Engine/TemplateEngine.cs` |
| `WPFCli/Output/ICS.DynamicTest/`（整个目录） |
| `WPFCli/Output/ICS.TestDemo1/`（整个目录） |
| `WPFCli/Output/ICS.TestDemo2/`（整个目录） |
| `WPFCli/Output/ICS.TestVerify/`（整个目录） |
| `WPFCli/Output/TestDemo2/`（整个目录） |

### 保留文件

| 路径 | 说明 |
|---|---|
| `WPFCli/Engine/FileWriter.cs` | 通用文件工具 |
| `WPFCli/WPFCli.csproj` | 已是 net8.0，无需改动 |

---

## 参考的 ExeBuilder 代码位置

| 复用逻辑 | 来源 | 目标 |
|---|---|---|
| Obfuscar 5 级检测 | `E:\ExeBuilder\Services\BuildService.cs`（FindObfuscar） | `Obfuscator.FindObfuscar()` |
| Obfuscar XML 生成（WPF 安全策略） | `BuildService.cs:497-566` `BuildObfuscarXml` | `Obfuscator.BuildObfuscarXml()` |
| AssemblySearchPath 添加 | `BuildService.cs:569-609` `AddAssemblySearchPaths` | `Obfuscator.AddAssemblySearchPaths()` |
| PE 头检测（IsManagedAssembly） | `BuildService.cs:640-702` `TryGetPEInfo` | `Obfuscator.IsManagedAssembly()` |
| InnoSetup .iss 生成 | `BuildService.cs:707-785` `GenerateInnoScript` | `InstallerPackager.GenerateInnoScript()` |
| 版本号读取 + 递增 | `E:\ExeBuilder\Config\VersionManager.cs:1-162` | `VersionManager.cs`（改为递增 patch 而非 build） |
| ISCC.exe 路径检测 | `BuildService.cs`（FindInnoSetup） | `InstallerPackager.FindInnoSetup()` |

---

## 验证方法（端到端测试）

1. **构建 WPFCli 自身**
   ```
   cd e:\WPFCli\WPFCli
   dotnet build
   ```

2. **运行向导**
   ```
   dotnet run --project e:\WPFCli\WPFCli
   ```
   输入：
   - 项目代号：`PT01`
   - 是否混淆：`y`
   - 是否打包：`y`

3. **验证模板替换**
   - 检查 `Output\PT01\PCBA.sln` 是否变为 `Output\PT01\PT01.sln`
   - 检查 `src\08.App\PCBA.App\` 是否变为 `src\08.App\PT01.App\`
   - 检查 `src\libs\DeviceLink\` 下命名空间未变（仍为 `PCBA.Devices.*` 之类——**不对，应该是 PS02 相关不动**）
   - 检查 `docs\PS02整机检测需求表.xlsx` 文件名不变（PS02 是设备代号）
   - 检查 `PT01.Infrastructure.dll` 命名空间正确

4. **验证编译**
   - `dotnet build Output\PT01\PT01.sln -c Release`
   - 确认 `Output\PT01\src\08.App\PT01.App\bin\Release\net8.0-windows10.0.19041.0\PT01.App.exe` 生成

5. **验证脚本生成**
   - `Output\PT01\obfuscate.ps1` 存在且路径正确
   - `Output\PT01\package.ps1` 存在且路径正确

6. **验证脚本执行**
   - 交互式确认执行混淆 → 检查 DLL 是否被混淆（可用 ILSpy 查看）
   - 交互式确认执行打包 → 检查 `Output\PT01\installer\PT01-Setup-1.0.1.exe` 生成

7. **回归测试**
   - 重新运行一遍，确认版本号自动递增为 `1.0.2`

---

## 实施顺序（建议分批 commit）

1. **批次 1：模型与配置** — `TemplateConfig.cs` + `BuildOptions.cs` + `template.config.json`
2. **批次 2：核心引擎** — `TemplateBuilder.cs` + `ProjectCompiler.cs` + `VersionManager.cs`
3. **批次 3：脚本生成** — `Obfuscator.cs` + `InstallerPackager.cs` + `PowerShellScriptBuilder.cs` + `PowerShellRunner.cs`
4. **批次 4：主流程** — 重写 `InteractiveWizard.cs` + `Program.cs` + `EnvironmentChecker.cs`
5. **批次 5：清理** — 删除旧 Generators/、旧 Models、旧 Output 项目
6. **批次 6：验证** — 端到端测试 + 修复问题
