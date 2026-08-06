# 模板清理与构建流程增强 — 延续实施计划

## Context（背景与动机）

本计划延续 `Template-Cleanup-And-Build-Enhancement.md`（已审批通过，部分执行）。原计划共 14 步，前 6 步已完成，步骤 7 部分完成（5 个文件中已清理 4 个），步骤 8-14 待执行。本文件聚焦剩余工作，所有关键设计决策（PS02 完全移除、MSBuild Target 集成、Directory.Build.props 统一版本）均已在原计划中由用户确认，无需再次澄清。

用户原始三项诉求：
1. 移除模板中和 PS02 相关的无用代码
2. 检查现有逻辑，将混淆/打包集成到模板 csproj，使其在 dotnet build 成功后自动执行 PS1
3. 完善版本号及更新记录（统一到 Directory.Build.props，附带审计元数据）

---

## 当前状态盘点

### 已完成（无需重复执行）

| 步骤 | 动作 | 状态 |
|------|------|------|
| 1 | 删除 PS02 相关 5 个文件/目录 | ✓ 已删除（验证：`Test-Path` 全部 False） |
| 2 | [PCBA.Devices.csproj](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices/PCBA.Devices.csproj) 移除 DeviceLink.Device.PS02 引用 | ✓ 已移除（仅剩 ZQWL/DPSEX/ConST685 三个引用） |
| 3 | [MainViewModel.cs](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/MainViewModel.cs) 删除 PS02_Board 自动加载 | ✓ 已删除（构造器以 `RecomputeAllExpanded();` 结束） |
| 4 | [ManifestMaintenanceViewModel.cs](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/ManifestMaintenanceViewModel.cs) 删除 PS02_Board 自动加载 | ✓ 已删除（构造器以 `RefreshList(null);` 结束） |
| 5 | [ResultDbContext.cs](file:///e:/WPFCli/Template/src/02.Infrastructure/PCBA.Infrastructure/Data/ResultDbContext.cs) 表名通用化 | ✓ 已改（`test_data` / `test_data_details`，注释已通用化） |
| 6 | [template.config.json](file:///e:/WPFCli/Template/template.config.json) 移除 `reservedNames` | ✓ 已移除 |
| 7a | [Manifest.cs](file:///e:/WPFCli/Template/src/01.Core/PCBA.Core.Abstractions/Manifest.cs) 清理 PS02 注释 | ✓ 已改（`PT01_Board` / `"PT01"`） |
| 7b | [ConnectionSettings.cs](file:///e:/WPFCli/Template/src/01.Core/PCBA.Core.Abstractions/ConnectionSettings.cs) 清理 PS02 注释 | ✓ 已改（`共享设备默认集合`） |
| 7c | [IStandardBox.cs](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices.Abstractions/IStandardBox.cs) 清理 PS02 注释 | ✓ 已改（`测试平台`） |
| 7d | [IDutDevice.cs](file:///e:/WPFCli/Template/src/03.Devices/PCBA.Devices.Abstractions/IDutDevice.cs) 清理 PS02 注释 | ✓ 已改（`具体型号见各产品驱动`） |

### 待执行步骤

| 步骤 | 动作 | 验证 |
|------|------|------|
| 7e | [ConnectionConfigViewModel.cs](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/ConnectionConfigViewModel.cs) 清理 3 处 PS02 注释 | 编译通过 |
| 8 | `dotnet build PCBA.sln -c Release` 验证模板编译 | 0 errors |
| 9 | [PCBA.App.csproj](file:///e:/WPFCli/Template/src/08.App/PCBA.App/PCBA.App.csproj) 添加 MSBuild Target 自动执行 PS1 | csproj 语法正确 |
| 10 | [Directory.Build.props](file:///e:/WPFCli/Template/Directory.Build.props) 添加版本号 + 审计元数据 | 编译通过 |
| 11 | [VersionManager.cs](file:///e:/WPFCli/WPFCli/Engine/VersionManager.cs) 修复 DetectVersion/WriteVersion | WPFCli 编译通过 |
| 12 | [Program.cs](file:///e:/WPFCli/WPFCli/Program.cs) 调整流程（PS1 生成提前到编译前，移除交互式执行） | WPFCli 编译通过 |
| 13 | `dotnet build WPFCli.csproj -c Release` 验证 WPFCli 编译 | 0 errors |
| 14 | 端到端验证：运行 WPFCli 生成 PT01 | 全流程通过 + 版本号正确写入 |

---

## 拟定修改

### 改造 1：完成步骤 7e（ConnectionConfigViewModel.cs 注释清理）

**文件**：[Template/src/07.UI/PCBA.UI.Shared/ViewModels/ConnectionConfigViewModel.cs](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/ConnectionConfigViewModel.cs)

**修改内容**（3 处）：

| 行号 | 原文 | 改为 |
|------|------|------|
| 14 | `「共享设备」(大气压模块（通讯方式可配置），所有针床通用) 与「PS02 被检设备」(按号位 = 拼版数)。` | `「共享设备」(大气压模块（通讯方式可配置），所有针床通用) 与「被检设备」(按号位 = 拼版数)。` |
| 185 | `「一键连接/断开」：同时操作共享设备（大气压模块）与选中的 PS02 被检设备。` | `「一键连接/断开」：同时操作共享设备（大气压模块）与选中的被检设备。` |
| 218 | `// PS02 被检设备（仅连接选中的工位）` | `// 被检设备（仅连接选中的工位）` |

**为什么**：彻底消除模板中 PS02 字样，避免误导新项目开发者。这三处仅是注释/说明文字，无功能影响。

---

### 改造 2：步骤 8 — 模板编译验证

执行 `dotnet build PCBA.sln -c Release` 验证 PS02 移除后模板仍可编译。基于反射自动注册机制，删除 PS02 后：
- `DutDriverRegistry.AutoRegisterFromAssembly` 注册表为空，回落到 `SimulatedDut`，不报错
- `TestStepsServiceCollectionExtensions.AddPcbaTestSteps` 处理器列表为空，不报错
- `JigCatalog` 扫描空 Manifests 目录，仅警告，不报错

预期结果：`Build succeeded. 0 Error(s)`。

---

### 改造 3：步骤 9 — PCBA.App.csproj 添加 MSBuild Target

**文件**：[Template/src/08.App/PCBA.App/PCBA.App.csproj](file:///e:/WPFCli/Template/src/08.App/PCBA.App/PCBA.App.csproj)

**修改内容**：在 `</Project>` 前插入两个 Target：

```xml
  <!-- ===== 编译后自动执行混淆和打包脚本（由 WPFCli 生成 PS1 后自动触发）===== -->
  <!-- 条件：仅 Release 配置 + PS1 文件存在时才执行，模板自身编译时 PS1 不存在故跳过 -->
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

**关键设计点**：
- **路径计算**：`$(MSBuildProjectDirectory)` = `Output\<代号>\src\08.App\<代号>.App\`，向上 3 层回到 `Output\<代号>\`，即 PS1 所在位置。与 [Obfuscator.cs:229](file:///e:/WPFCli/WPFCli/Engine/Obfuscator.cs#L229) 和 [InstallerPackager.cs:144](file:///e:/WPFCli/WPFCli/Engine/InstallerPackager.cs#L144) 生成的 PS1 路径一致（PS1 内 `$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path` 也指向同一目录）。
- **Condition 双重保护**：
  - WPFCli 用户选了混淆/打包 → 生成 PS1 → Target 执行
  - WPFCli 用户没选 → 不生成 PS1 → Target 跳过
  - 模板自身 `dotnet build PCBA.sln` → PS1 不存在 → Target 跳过，模板开发不受影响
- **执行顺序**：`RunObfuscateScript` 在 `Build` 后触发，`RunPackageScript` 在 `RunObfuscateScript` 后触发。混淆失败则 ExitCode 非零 → dotnet build 失败 → 打包 Target 不会执行（MSBuild 默认行为）。
- **仅 Release 配置触发**：Debug 调试时不执行 PS1，加快开发循环。

---

### 改造 4：步骤 10 — Directory.Build.props 添加版本号 + 审计元数据

**文件**：[Template/Directory.Build.props](file:///e:/WPFCli/Template/Directory.Build.props)

**修改内容**：扩展为统一版本号 + 构建审计元数据：

```xml
<Project>
  <PropertyGroup>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>

    <!-- ===== 统一版本号（所有 PCBA.* 项目共享）===== -->
    <!-- WPFCli 生成项目时自动递增 patch 段（1.0.0 → 1.0.1）-->
    <Version>1.0.0</Version>
    <AssemblyVersion>1.0.0.0</AssemblyVersion>
    <FileVersion>1.0.0.0</FileVersion>

    <!-- ===== 构建审计元数据（可选，由 WPFCli 在生成时回填）===== -->
    <!-- 留空表示模板原始版本；WPFCli 生成项目后会写入 ProjectCode、生成时间等 -->
    <BuildProjectCode></BuildProjectCode>
    <BuildBaseVersion></BuildBaseVersion>
    <BuildGeneratedAt></BuildGeneratedAt>
  </PropertyGroup>
</Project>
```

**为什么新增三个 Build* 属性**：
- `<BuildProjectCode>`：记录由 WPFCli 生成的项目代号（如 PT01），便于运行时通过反射读取，做日志/审计/错误上报。
- `<BuildBaseVersion>`：记录所基于的模板版本（即递增前的版本，如 1.0.0），便于追溯该项目的"代际"。
- `<BuildGeneratedAt>`：记录由 WPFCli 生成的 UTC 时间戳，便于审计。

这三项是"可选审计字段"——即使为空也不影响编译，但在生成项目时由 WPFCli 填充后即可提供完整溯源链：`BuildProjectCode + Version + BuildBaseVersion + BuildGeneratedAt`。

**为什么用 Directory.Build.props 而非各 csproj**：
- MSBuild 自动向上查找，所有子项目继承同一版本号
- WPFCli 修改此文件一处，即可同步全部项目版本
- 不含 "PCBA" 字符串，模板替换后仍生效

---

### 改造 5：步骤 11 — VersionManager.cs 修复 DetectVersion/WriteVersion

**文件**：[WPFCli/Engine/VersionManager.cs](file:///e:/WPFCli/WPFCli/Engine/VersionManager.cs)

**当前 bug**：
- `DetectVersion` 从主项目 csproj 读取 `<Version>` 标签，但模板 csproj 没有此标签 → 返回 null → 默认 "1.0.0"
- `WriteVersion` 遍历所有 csproj 并替换 `<Version>` 标签，但模板 csproj 没有此标签 → 替换不发生 → 版本号未写入

**修复方案**：改为统一从 `Directory.Build.props` 读写版本号。

#### 5.1 修改 `DetectVersion`

优先从 `Directory.Build.props` 读取，csproj 仅作向后兼容兜底：

```csharp
public static string? DetectVersion(string templatePath, string mainProjectName)
{
    try
    {
        // 优先从 Directory.Build.props 读取（统一版本管理）
        var propsFile = Path.Combine(templatePath, "Directory.Build.props");
        if (File.Exists(propsFile))
        {
            var version = ReadVersionFromProps(propsFile);
            if (!string.IsNullOrEmpty(version)) return version;
        }
        // 兜底：从主项目 csproj 读取（向后兼容旧模板）
        var csproj = Path.Combine(templatePath, "src", "08.App", mainProjectName, $"{mainProjectName}.csproj");
        return ReadVersionFromCsproj(csproj);
    }
    catch
    {
        return null;
    }
}

private static string? ReadVersionFromProps(string propsPath)
{
    try
    {
        if (!File.Exists(propsPath)) return null;
        var content = File.ReadAllText(propsPath);
        foreach (var tag in new[] { "Version", "AssemblyVersion", "FileVersion" })
        {
            var m = Regex.Match(
                content,
                $@"<{tag}>\s*(\d+\.\d+\.\d+(?:\.\d+)?)\s*</{tag}>",
                RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value;
        }
        return null;
    }
    catch
    {
        return null;
    }
}
```

#### 5.2 修改 `WriteVersion`

改为只修改 `Directory.Build.props`（不再遍历所有 csproj），同时回填审计元数据：

```csharp
public static void WriteVersion(string outputDir, string newVersion, string? projectCode = null, string? baseVersion = null)
{
    try
    {
        if (!Directory.Exists(outputDir)) return;
        var propsFile = Path.Combine(outputDir, "Directory.Build.props");
        if (!File.Exists(propsFile))
        {
            Console.WriteLine("[VersionManager] Directory.Build.props not found, skip version write");
            return;
        }

        var content = File.ReadAllText(propsFile);
        var updated = false;

        // 替换三个版本号标签
        foreach (var tag in new[] { "Version", "AssemblyVersion", "FileVersion" })
        {
            var pattern = $@"<{tag}>\s*[^<]*\s*</{tag}>";
            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
            {
                var value = tag == "Version" ? newVersion : $"{newVersion}.0";
                content = Regex.Replace(content,
                    pattern,
                    $"<{tag}>{value}</{tag}>",
                    RegexOptions.IgnoreCase);
                updated = true;
            }
        }

        // 回填审计元数据（BuildProjectCode / BuildBaseVersion / BuildGeneratedAt）
        if (!string.IsNullOrEmpty(projectCode))
        {
            content = ReplaceOrCreateTag(content, "BuildProjectCode", projectCode);
            updated = true;
        }
        if (!string.IsNullOrEmpty(baseVersion))
        {
            content = ReplaceOrCreateTag(content, "BuildBaseVersion", baseVersion);
            updated = true;
        }
        content = ReplaceOrCreateTag(content, "BuildGeneratedAt", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        updated = true;

        if (updated)
            File.WriteAllText(propsFile, content);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[VersionManager] WriteVersion warning: {ex.Message}");
    }
}

/// <summary>替换或新增一个 MSBuild 属性标签。若标签不存在则在 PropertyGroup 内追加。</summary>
private static string ReplaceOrCreateTag(string content, string tag, string value)
{
    var pattern = $@"<{tag}>\s*[^<]*\s*</{tag}>";
    if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
    {
        return Regex.Replace(content, pattern, $"<{tag}>{value}</{tag}>", RegexOptions.IgnoreCase);
    }
    // 标签不存在 → 在第一个 </PropertyGroup> 前插入
    var insertPattern = @"</PropertyGroup>";
    if (Regex.IsMatch(content, insertPattern, RegexOptions.IgnoreCase))
    {
        return Regex.Replace(content, insertPattern,
            $"  <{tag}>{value}</{tag}>\n  </PropertyGroup>",
            RegexOptions.IgnoreCase);
    }
    return content;
}
```

**好处**：
- 修复"模板无 Version 标签导致不写入"的 bug
- 不再遍历所有 csproj，避免误改 DeviceLink 等子项目
- 顺便回填审计元数据，便于运行时溯源

---

### 改造 6：步骤 12 — Program.cs 调整流程

**文件**：[WPFCli/Program.cs](file:///e:/WPFCli/WPFCli/Program.cs)

**当前流程**（[Program.cs:60-189](file:///e:/WPFCli/WPFCli/Program.cs#L60-L189)）：
1. 拷贝模板 + 替换
2. 写入版本号
3. 编译项目
4. 生成混淆脚本（可选）
5. 生成打包脚本（可选）
6. 交互式执行 .ps1（[Program.cs:135-184](file:///e:/WPFCli/WPFCli/Program.cs#L135-L184)）
7. 产物清单

**新流程**：
1. 拷贝模板 + 替换
2. 写入版本号（含审计元数据）
3. **生成混淆脚本（可选）** ← 提前到编译前
4. **生成打包脚本（可选）** ← 提前到编译前
5. 编译项目（MSBuild Target 自动执行 PS1）
6. 产物清单

**修改要点**：
- 将"生成混淆脚本"和"生成打包脚本"两块代码移到"编译项目"之前
- 调整步骤编号（1→拷贝、2→版本、3→混淆脚本、4→打包脚本、5→编译）
- 修改 `WriteVersion` 调用，传入 `projectCode` 和 `baseVersion` 参数
- 删除交互式执行 PS1 的代码块（约 50 行）
- 修改 `PrintArtifacts` 中的"下一步操作"提示，因为 PS1 已自动执行，无需再提示手动运行

#### 6.1 删除交互式执行代码

删除 [Program.cs:135-184](file:///e:/WPFCli/WPFCli/Program.cs#L135-L184) 这段代码：
```csharp
// 6. 交互式执行 .ps1（仅当编译成功时才提示执行，避免脚本运行时报错）
if (compileSuccess && opts.EnableObfuscation) { ... }
if (compileSuccess && opts.EnablePackaging) { ... }
```

#### 6.2 修改 WriteVersion 调用

在 [Program.cs:70-74](file:///e:/WPFCli/WPFCli/Program.cs#L70-L74) 修改为：

```csharp
// 步骤 2: 写入新版本号 + 审计元数据
RunStep(2, "写入版本号", stepSw, () =>
{
    VersionManager.WriteVersion(opts.OutputDir, opts.Version,
        projectCode: opts.ProjectCode,
        baseVersion: baseVersion);
    Console.WriteLine($"    版本号写入: {opts.Version}");
});
```

#### 6.3 调整步骤顺序

将原步骤 4（混淆脚本）和步骤 5（打包脚本）移到编译步骤之前：

```csharp
// 步骤 3 (可选): 生成混淆脚本（提前到编译前，使 MSBuild Target 在编译时能找到 PS1）
if (opts.EnableObfuscation)
{
    RunStep(3, "生成混淆脚本 (obfuscate.ps1)", stepSw, () =>
    {
        var ps1 = Obfuscator.GenerateObfuscatePs1(opts);
        var ps1Path = Path.Combine(opts.OutputDir, "obfuscate.ps1");
        File.WriteAllText(ps1Path, ps1, new UTF8Encoding(false));
        Console.WriteLine($"    脚本已生成: {ps1Path}");
    });
}

// 步骤 4 (可选): 生成打包脚本（提前到编译前）
if (opts.EnablePackaging)
{
    RunStep(opts.EnableObfuscation ? 4 : 3, "生成打包脚本 (package.ps1)", stepSw, () =>
    {
        var ps1 = InstallerPackager.GeneratePackagePs1(opts);
        var ps1Path = Path.Combine(opts.OutputDir, "package.ps1");
        File.WriteAllText(ps1Path, ps1, new UTF8Encoding(false));
        Console.WriteLine($"    脚本已生成: {ps1Path}");
    });
}

// 步骤 5: 编译项目（MSBuild Target 会自动执行 obfuscate.ps1/package.ps1）
RunStep(opts.EnableObfuscation || opts.EnablePackaging ? 5 : 3, "编译项目 (dotnet build)", stepSw, () => { ... });
```

#### 6.4 编译失败处理

由于 PS1 现在在编译过程中执行（MSBuild Target），编译失败可能是：
- 模板代码本身编译错误（C# 编译错误）
- PS1 执行失败（如 Obfuscar 未安装、ISCC 未安装）

WPFCli 现有逻辑会捕获编译退出码，输出编译日志。需要修改提示信息，告知用户若失败可能来自 PS1：

```csharp
if (!compileSuccess)
{
    Console.WriteLine();
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  [!] 构建失败。可能原因：");
    Console.WriteLine("      1. 模板代码编译错误（请查看上方错误日志）");
    Console.WriteLine("      2. 混淆/打包脚本执行失败（请检查 Obfuscar/ISCC 是否安装）");
    Console.WriteLine("      修复后到 Output 目录手动执行 dotnet build 重试。");
    Console.ResetColor();
}
```

#### 6.5 修改 PrintArtifacts 提示

由于 PS1 已自动执行，"下一步操作"提示需改为：

```csharp
Console.WriteLine("  下一步操作：");
Console.WriteLine($"    cd \"{opts.OutputDir}\"");
Console.WriteLine("    # 如需重新混淆/打包，可手动运行：");
if (opts.EnableObfuscation)
    Console.WriteLine("    .\\obfuscate.ps1");
if (opts.EnablePackaging)
    Console.WriteLine("    .\\package.ps1");
```

---

## 假设与决策

1. **PS02 移除后模板可编译**：基于反射自动注册机制，删除 PS02 代码后模板仍能编译。通用 `SimulatedDut` 作为回落驱动。
2. **MSBuild Target 用 Condition 双重保护**：PS1 不存在时 Target 跳过，不影响模板自身编译，也不影响 Debug 配置下的快速开发。
3. **版本号统一到 Directory.Build.props**：不再在每个 csproj 中声明 Version，一处修改全局生效。
4. **审计元数据为可选字段**：`<BuildProjectCode>` 等三个字段留空时不影响编译，由 WPFCli 在生成项目时回填。
5. **WPFCli 流程调整**：PS1 生成提前到编译前，使编译时 MSBuild Target 能找到 PS1 并自动执行。
6. **移除交互式 PS1 执行**：原本需要用户确认两次（混淆/打包），改为编译时自动执行，简化用户操作。
7. **不生成 CHANGELOG.md**：版本号递增 + BuildGeneratedAt 时间戳已提供基本审计能力。如需更详细的变更日志，可由 git commit message 承担，本计划不实施。

---

## 实施步骤（剩余 9 步）

| 步骤 | 动作 | 验证 |
|------|------|------|
| 7e | 修改 [ConnectionConfigViewModel.cs](file:///e:/WPFCli/Template/src/07.UI/PCBA.UI.Shared/ViewModels/ConnectionConfigViewModel.cs) 第 14/185/218 行 PS02 → 通用描述 | 文本检查 |
| 8 | `cd e:\WPFCli\Template && dotnet build PCBA.sln -c Release` | 0 errors |
| 9 | 修改 [PCBA.App.csproj](file:///e:/WPFCli/Template/src/08.App/PCBA.App/PCBA.App.csproj) 在 `</Project>` 前插入两个 MSBuild Target | csproj XML 合法 |
| 10 | 修改 [Directory.Build.props](file:///e:/WPFCli/Template/Directory.Build.props) 添加 Version/AssemblyVersion/FileVersion + 三个审计字段 | 编译通过 |
| 11 | 修改 [VersionManager.cs](file:///e:/WPFCli/WPFCli/Engine/VersionManager.cs)：DetectVersion 优先读 Directory.Build.props；WriteVersion 改为写 Directory.Build.props + 回填审计元数据 | WPFCli 编译通过 |
| 12 | 修改 [Program.cs](file:///e:/WPFCli/WPFCli/Program.cs)：PS1 生成提前到编译前；删除交互式 PS1 执行；更新 WriteVersion 调用；更新编译失败提示与产物清单 | WPFCli 编译通过 |
| 13 | `cd e:\WPFCli\WPFCli && dotnet build -c Release` | 0 errors |
| 14 | 端到端验证：`Remove-Item -Recurse -Force e:\WPFCli\Output\PT01; dotnet run --project e:\WPFCli\WPFCli\WPFCli.csproj -c Release`（输入 PT01，混淆=否，打包=否）| 全流程通过 + Directory.Build.props 含 1.0.1 + 审计元数据 |

---

## 验证步骤

### 1. 模板编译验证（步骤 8）

```powershell
cd e:\WPFCli\Template
dotnet build PCBA.sln -c Release
```
预期：`Build succeeded. 0 Error(s)`，无 PS02 相关警告。

### 2. WPFCli 编译验证（步骤 13）

```powershell
cd e:\WPFCli\WPFCli
dotnet build -c Release
```
预期：`0 errors, 0 warnings`。

### 3. 端到端验证（步骤 14，无混淆无打包）

```powershell
Remove-Item -Recurse -Force e:\WPFCli\Output\PT01
dotnet run --project e:\WPFCli\WPFCli\WPFCli.csproj -c Release
# 交互输入: PT01 → n → n → y
```
预期：
- 步骤 1：拷贝模板 + 替换 PCBA→PT01
- 步骤 2：写入版本号 1.0.0 → 1.0.1 到 `Output\PT01\Directory.Build.props`
- 步骤 3：编译成功（PS1 不存在，MSBuild Target 跳过）
- 产物清单显示 PT01.App.exe
- `Output\PT01\Directory.Build.props` 包含 `<Version>1.0.1</Version>` 和 `<BuildProjectCode>PT01</BuildProjectCode>` 等审计字段

### 4. 版本号写入验证

```powershell
Get-Content e:\WPFCli\Output\PT01\Directory.Build.props | Select-String "Version|Build"
```
预期输出：
```
    <Version>1.0.1</Version>
    <AssemblyVersion>1.0.1.0</AssemblyVersion>
    <FileVersion>1.0.1.0</FileVersion>
    <BuildProjectCode>PT01</BuildProjectCode>
    <BuildBaseVersion>1.0.0</BuildBaseVersion>
    <BuildGeneratedAt>2026-07-18T...
```

### 5. MSBuild Target 自动执行验证（可选，需 Obfuscar + ISCC 已安装）

```powershell
Remove-Item -Recurse -Force e:\WPFCli\Output\PT02
dotnet run --project e:\WPFCli\WPFCli\WPFCli.csproj -c Release
# 交互输入: PT02 → y → y → y
```
预期：
- 步骤 3：生成 obfuscate.ps1
- 步骤 4：生成 package.ps1
- 步骤 5：编译开始 → `Build succeeded` → MSBuild 自动执行 obfuscate.ps1 → 自动执行 package.ps1
- 编译日志可见 `====== 执行混淆脚本 ======` 和 `====== 执行打包脚本 ======`
- 产物清单显示 PT02.App.exe + 混淆后的 DLL + 安装包 .exe

---

## 风险与回退

- **风险**：删除交互式 PS1 执行后，用户失去"是否运行 PS1"的二次确认。
  - **缓解**：用户在向导中已选择是否启用混淆/打包，未启用则不生成 PS1，Target 自动跳过。
- **风险**：MSBuild Target 执行 PS1 失败会导致 dotnet build 失败。
  - **缓解**：用户可选混淆=否、打包=否 来跳过 PS1。失败时提示信息已明确指出可能原因。
- **风险**：VersionManager 修改后可能影响已有 Output 产物。
  - **缓解**：每次生成前清理 Output 目录（验证步骤已包含 `Remove-Item`）。
- **风险**：Directory.Build.props 新增的 Build* 字段在某些 MSBuild 版本可能引发警告。
  - **缓解**：这些是无 Action 的元数据属性，MSBuild 会忽略，不参与编译。如出现警告，可加 `<BuildProjectCode Condition="'$(BuildProjectCode)' == ''" />` 等条件化处理。
- **回退**：所有修改均为增量修改，可通过 git revert 恢复。
