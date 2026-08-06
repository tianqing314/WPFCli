# WPFCli（TestRig CLI）多业务模板化改造方案

> 目标：把 TestRig CLI 从"单 PCBA 模板"改造为"多业务类型模板脚手架"，
> 模板按业务分类放置（Common 公共骨架 + Complete/Machine/Inspect/Aging 业务模板 + Dynamic 预留），
> 生成时"Common + 业务模板"合并拷贝 → 占位符替换 → 重命名 → 版本管理 → 编译验证。
> 对应 Bots.TestBench 拆分方案中的"脚手架模板化"落地。

---

## 一、改造目标

1. `Template\` 按业务类型分类：`Common`（公共骨架）、`Complete`（组件测试）、`Machine`（整机测试）、`Inspect`（出厂检验）、`Aging`（老化）、`Dynamic`（动态工装预留）。
2. 交互向导**第一步选择业务类型**（Common 自动合入，不单独选择；Dynamic 显示"预留"不可选）。
3. 生成流程变为 **Common + 业务模板合并拷贝**：先拷 Common 全量骨架，再拷业务模板覆盖/追加（同名文件覆盖），最后统一替换占位符、重命名、写版本、编译。
4. 保留原有"直接生成完整可编译项目"的能力（业务模板为空时，合并结果 = Common 内容）。

## 二、模板目录新布局

```
G:\WPFCli\
├── Template\
│   ├── template.config.json        # 全局配置（placeholder 等全局字段，保留）
│   ├── Common\                     # 公共骨架 = 现有 PCBA 模板内容整体迁入
│   │   ├── Directory.Build.props   # 版本号统一在此（生成时读取并递增）
│   │   ├── PCBA.sln                # 重命名阶段 → {代号}.sln
│   │   ├── src\                    # 8 层架构（01.Core ~ 08.App + libs/DeviceLink）
│   │   ├── docs\  tools\
│   │   └── template.config.json    # businessType=common（新增）
│   ├── Complete\                   # 组件测试模板
│   │   ├── template.config.json    # businessType=complete
│   │   └── README.md
│   ├── Machine\                    # 整机测试模板（同构）
│   ├── Inspect\                    # 出厂检验模板（同构）
│   ├── Aging\                      # 老化模板（同构）
│   └── Dynamic\                    # 动态工装模板（预留）
│       └── template.config.json    # businessType=dynamic, disabled=true
└── Output\<代号>\                  # 生成产物（不变）
```

## 三、代码改动清单

### 3.1 `WPFCli\Models\TemplateConfig.cs` — 新增 2 个属性

在 `ReservedNames` 之后追加：

```csharp
    /// <summary>业务类型标识（common/complete/machine/inspect/aging/dynamic）。</summary>
    [JsonPropertyName("businessType")]
    public string BusinessType { get; set; } = "";

    /// <summary>预留模板标记 —— 为 true 时向导中显示但不可选择（如动态工装预留）。</summary>
    [JsonPropertyName("disabled")]
    public bool Disabled { get; set; }
```

### 3.2 `WPFCli\Models\BuildOptions.cs` — 新增 3 个成员

在 `TemplatePath` 之后追加：

```csharp
    /// <summary>业务模板目录绝对路径（Template\&lt;业务类型&gt;）。</summary>
    public string BusinessTemplatePath { get; set; } = "";

    /// <summary>业务模板元数据（可覆盖全局配置的 description/exclude 等）。</summary>
    public TemplateConfig BusinessTemplate { get; set; } = new();

    /// <summary>业务类型标识（complete/machine/inspect/aging）。</summary>
    public string BusinessType => Path.GetFileName(BusinessTemplatePath);
```

### 3.3 `WPFCli\Engine\InteractiveWizard.cs` — 向导增加业务类型选择

1. **`Run` 方法**：向导顺序改为 `[1] 业务类型 → [2] 项目代号 → [3] 混淆 → [4] 打包`；在设置 `ProjectCode` 前先调用 `PromptBusinessType(templatePath, opts)`。

2. **新增 `PromptBusinessType`**（核心逻辑）：

```csharp
    /// <summary>选择业务类型：枚举 Template\ 下含 template.config.json 的子目录（排除 Common）。</summary>
    private static string? PromptBusinessType(string templatePath, BuildOptions opts)
    {
        var choices = new List<(string Dir, TemplateConfig Cfg)>();
        foreach (var dir in Directory.GetDirectories(templatePath))
        {
            var cfgPath = Path.Combine(dir, "template.config.json");
            if (!File.Exists(cfgPath)) continue;
            var name = Path.GetFileName(dir);
            if (name.Equals("Common", StringComparison.OrdinalIgnoreCase)) continue; // 公共骨架自动合入
            var cfg = JsonSerializer.Deserialize<TemplateConfig>(File.ReadAllText(cfgPath));
            if (cfg == null) continue;
            choices.Add((name, cfg));
        }
        if (choices.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [ERROR] 未找到任何业务模板（Template\\<业务>\\template.config.json）。");
            Console.ResetColor();
            return null;
        }

        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  ▸ 选择业务类型");
        Console.ResetColor();
        for (int i = 0; i < choices.Count; i++)
        {
            var (dir, cfg) = choices[i];
            var mark = cfg.Disabled ? "  [预留]" : "";
            Console.ForegroundColor = cfg.Disabled ? ConsoleColor.DarkGray : ConsoleColor.Green;
            Console.Write($"    {i + 1}. {dir}");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  {cfg.Description}{mark}");
            Console.ResetColor();
        }

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"  > 请选择业务类型 [1-{choices.Count}]: ");
            Console.ResetColor();
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var idx) && idx >= 1 && idx <= choices.Count)
            {
                var (dir, cfg) = choices[idx - 1];
                if (cfg.Disabled)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"    \"{dir}\" 为预留模板，暂不可用，请选择其他业务类型。");
                    Console.ResetColor();
                    continue;
                }
                opts.BusinessTemplatePath = Path.Combine(templatePath, dir);
                opts.BusinessTemplate = cfg;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"    ✓ 业务类型: {dir}（{cfg.Description}）");
                Console.ResetColor();
                return dir;
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("    输入无效，请输入 1-" + choices.Count + " 之间的数字。");
            Console.ResetColor();
        }
    }
```

3. **Banner / Summary**：Banner 增加一行 `BoxLine($"业务模板: {opts.BusinessType}")`（在 `Run` 选择完成后无需改 Banner，改 Summary 即可）；`PrintSummary` 增加 `PrintField("业务类型", opts.BusinessType);`。

4. 文件头部补充 `using System.Text.Json;`。

### 3.4 `WPFCli\Engine\TemplateBuilder.cs` — 合并拷贝

1. **`Build` 方法改为合并流程**：

```csharp
    public static void Build(BuildOptions opts, Action<string>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(opts);

        if (!Directory.Exists(opts.TemplatePath))
            throw new DirectoryNotFoundException($"模板目录不存在: {opts.TemplatePath}");
        if (string.IsNullOrEmpty(opts.BusinessTemplatePath) || !Directory.Exists(opts.BusinessTemplatePath))
            throw new DirectoryNotFoundException($"业务模板目录不存在: {opts.BusinessTemplatePath}");

        // 0. 清理输出目录（如已存在）
        if (Directory.Exists(opts.OutputDir))
        {
            onProgress?.Invoke($"  清理已存在的输出目录: {opts.OutputDir}");
            Directory.Delete(opts.OutputDir, recursive: true);
        }
        Directory.CreateDirectory(opts.OutputDir);

        // 排除规则：全局 ∪ 业务模板（取并集）
        var excludeCopy = Merge(opts.Template.ExcludeFromCopy, opts.BusinessTemplate.ExcludeFromCopy);
        var excludeReplace = Merge(opts.Template.ExcludeFromReplacement, opts.BusinessTemplate.ExcludeFromReplacement);

        // 1. 拷贝公共骨架（Common）
        var commonPath = Path.Combine(opts.TemplatePath, "Common");
        if (!Directory.Exists(commonPath))
            throw new DirectoryNotFoundException($"公共模板目录不存在: {commonPath}");
        onProgress?.Invoke($"  拷贝公共模板: {commonPath} → {opts.OutputDir}");
        CopyDirectory(commonPath, opts.OutputDir, excludeCopy);

        // 2. 拷贝业务模板（覆盖合并：同名文件覆盖，新文件追加）
        onProgress?.Invoke($"  拷贝业务模板: {opts.BusinessTemplatePath} → {opts.OutputDir}");
        CopyDirectory(opts.BusinessTemplatePath, opts.OutputDir, excludeCopy);

        // 3. 替换文件内容（排除指定目录，跳过二进制；排除 template.config.json）
        onProgress?.Invoke($"  替换占位符 '{opts.Template.Placeholder}' → '{opts.ProjectCode}'");
        ReplaceContentInFiles(opts.OutputDir, opts, excludeReplace);

        // 4. 重命名文件和文件夹（先深后浅）
        onProgress?.Invoke($"  重命名文件和文件夹");
        RenameFilesAndDirectories(opts.OutputDir, opts);
    }
```

2. **新增 `Merge` 辅助**：

```csharp
    private static List<string> Merge(List<string> a, List<string> b)
    {
        var set = new HashSet<string>(a, StringComparer.OrdinalIgnoreCase);
        foreach (var x in b) set.Add(x);
        return set.ToList();
    }
```

3. **`CopyDirectory` 增加跳过 `template.config.json`**：在文件拷贝循环开头加：

```csharp
            if (fileName.Equals("template.config.json", StringComparison.OrdinalIgnoreCase))
                continue;   // 模板元数据不进入生成产物
```

4. **`ReplaceContentInFiles` 的排除目录参数化**：签名改为
   `private static void ReplaceContentInFiles(string rootDir, BuildOptions opts, List<string> excludeReplace)`
   内部 `IsInExcludedPath(relPath, excludeReplace)` 用传入的合并结果，不再直接读 `opts.Template.ExcludeFromReplacement`。

> 注意：业务模板若想覆盖 Common 的某个文件，把同名文件放到业务模板的相对路径即可（如
> `Template\Complete\src\07.UI\...\MainWindow.xaml` 覆盖 Common 的同路径文件）。

### 3.5 `WPFCli\Program.cs` — 版本从 Common 读取

第 45 行附近：

```csharp
        // 版本号从公共模板（Common）读取 —— 版本统一在 Common\Directory.Build.props 管理
        var commonPath = Path.Combine(templatePath, "Common");
        var baseVersion = VersionManager.DetectVersion(commonPath, templateConfig.MainProjectName) ?? "1.0.0";
```

其余不变（`opts.BusinessTemplatePath` 已由向导填入）。

### 3.6 模板目录迁移与新增文件

1. **迁移**（Move，勿复制）：
   - `Template\src` → `Template\Common\src`
   - `Template\Directory.Build.props` → `Template\Common\Directory.Build.props`
   - `Template\PCBA.sln` → `Template\Common\PCBA.sln`
   - `Template\docs` → `Template\Common\docs`
   - `Template\tools` → `Template\Common\tools`

2. **`Template\template.config.json`（全局）** 保留并精简为：

```json
{
  "placeholder": "PCBA",
  "description": "TestRig 多业务模板脚手架",
  "targetFramework": "net8.0-windows10.0.19041.0",
  "configuration": "Release",
  "mainProjectName": "PCBA.App",
  "excludeFromCopy": [ "bin", "obj", ".reasonrix", ".vs", ".git" ],
  "excludeFromReplacement": [ "src/libs/DeviceLink", "tools", "docs" ],
  "obfuscationTargets": [ "Infrastructure", "Devices", "TestSteps", "Core.Engine" ],
  "reservedNames": [ "PS02" ]
}
```

3. **`Template\Common\template.config.json`（新增）**：

```json
{
  "description": "公共骨架（所有业务共用，自动合入）",
  "businessType": "common"
}
```

4. **业务模板 config**（以 Complete 为例，Machine/Inspect/Aging 同构，改 description/businessType）：

```json
{
  "description": "组件测试模板",
  "businessType": "complete",
  "excludeFromCopy": [],
  "excludeFromReplacement": []
}
```

5. **`Template\Dynamic\template.config.json`（预留）**：

```json
{
  "description": "动态工装模板（预留，暂不可用）",
  "businessType": "dynamic",
  "disabled": true,
  "excludeFromCopy": [],
  "excludeFromReplacement": []
}
```

6. **各业务目录 README.md**（说明该目录后续放什么）：
   - `Complete\README.md`：组件测试工装驱动（TestTool 类）、组件测试任务 JSON 与脚本骨架
   - `Machine\README.md`：整机自检（SelfCheck）任务 JSON 与脚本骨架
   - `Inspect\README.md`：出厂检验任务（合格证/出厂日期/恢复出厂）与数据契约
   - `Aging\README.md`：老化任务（AgingPosition 位置控制等）
   - `Dynamic\README.md`：动态工装（DynamicStandardTestBench 等），预留

> 注意：业务模板内容暂为空（仅 config + README），合并结果 = Common 骨架，仍可编译。业务代码由后续按需填充。

### 3.7 文档更新（可选，最后做）

- `G:\WPFCli\README.md`：新增"业务模板"章节与用法示例（选择业务类型）。
- `G:\WPFCli\SKILL.md`：更新触发条件与生成流程描述。

## 四、验证步骤

```powershell
# 1. 编译 CLI
cd G:\WPFCli
dotnet build WPFCli\WPFCli.csproj

# 2. 生成测试项目（管道输入：业务类型序号 + 代号 + 混淆 n + 打包 y）
#    例如选择 Complete（序号 1）、代号 TestApp
"1`nTestApp`nn`ny" | dotnet run --project WPFCli

# 3. 检查产物：不应残留 template.config.json，src 完整，TestApp.sln 存在
Get-ChildItem Output\TestApp -Recurse -Filter template.config.json   # 应无输出
Test-Path Output\TestApp\TestApp.sln                                  # 应为 True

# 4. 编译生成的解决方案
dotnet build Output\TestApp\TestApp.sln

# 5. 验证预留模板不可选：输入序号选择 Dynamic（disabled=true）应被拒绝
```

## 五、注意事项

1. **Common 是完整可编译骨架**：业务模板为空时，合并结果 = Common 内容，原"直接生成完整项目"能力不丢。
2. **同名覆盖**：业务模板与 Common 同名文件 → 业务模板覆盖（`File.Copy` overwrite:true 已支持）；不同名 → 共存。
3. **占位符统一**：全局 `placeholder=PCBA`；业务模板代码中同样使用 `PCBA` 占位，生成时统一替换。业务 config 不要覆盖 placeholder。
4. **`template.config.json` 不进入产物**：拷贝阶段跳过，避免输出目录残留多个模板元数据。
5. **Dynamic 预留**：`disabled=true`，向导中显示但不可选。
6. **回归**：改造后需验证原 8 层结构、混淆/打包脚本、版本递增、审计元数据回填均不受影响（这些逻辑不涉及模板路径，理论上无需改动）。
