using System.Text;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 上传方案生成器 —— 根据向导选择生成发布/CI 方案文件到 Output\&lt;代号&gt;\：
///   - 仅 FTP：自写实现 publish_ftp.ps1（编译 → 打包 → FTP 上传三件套）
///   - GitLab + FTP：参考 G:\CommonDebugTool\upgrade 方案生成 .gitlab-ci.yml + upgrade\ 脚本
///   - 仅 GitLab：生成 .gitlab-ci.yml（编译 + tag，无 FTP 步骤）+ push_gitlab.ps1
/// FTP 地址写入脚本；用户名和密码仅在发布时通过 TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD 注入。
/// 目标框架 / 编译配置一律取 opts.Template.* 动态拼接，避免模板升级后脚本失效。
/// </summary>
public static class UploadScriptGenerator
{
    /// <summary>是否有任何上传方案需要生成。</summary>
    public static bool IsEnabled(BuildOptions opts) => opts.EnableGitLab || opts.EnableFtp;

    /// <summary>生成上传方案文件到输出目录。</summary>
    public static void Generate(BuildOptions opts, Action<string>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(opts);
        if (!IsEnabled(opts)) return;

        // 版本三件套（AutoDeployConfig.xml + VersionsInfo.json）—— FTP 上传需要，GitLab CI 也需要
        WriteVersionFiles(opts);

        if (opts.EnableGitLab && opts.EnableFtp)
        {
            // 既上传 GitLab 又上传 FTP：参考 G:\CommonDebugTool\upgrade 完整方案
            onProgress?.Invoke($"  生成 GitLab CI/CD + FTP 发布方案（参考 CommonDebugTool/upgrade）");
            WriteGitLabCi(opts, includeFtp: true);
            WriteCiUpdateVersionsPs1(opts);
            WritePublishPs1(opts);
            WriteLocalTestCiPs1(opts);
            WritePushGitlabPs1(opts);
            WriteGitIgnore(opts);
        }
        else if (opts.EnableGitLab)
        {
            // 仅 GitLab：CI 编译 + tag（无 FTP 步骤）
            onProgress?.Invoke($"  生成 GitLab CI/CD 方案（仅编译，无 FTP）");
            WriteGitLabCi(opts, includeFtp: false);
            WritePushGitlabPs1(opts);
            WriteGitIgnore(opts);
        }
        else if (opts.EnableFtp)
        {
            // 仅 FTP：自写实现方案
            onProgress?.Invoke($"  生成 FTP 发布脚本（自写实现）");
            WriteFtpOnlyPublishPs1(opts);
        }
    }

    /// <summary>写入版本三件套初始文件（AutoDeployConfig.xml + VersionsInfo.json）。</summary>
    private static void WriteVersionFiles(BuildOptions opts)
    {
        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<AutoDeploy>
  <Version value=""{opts.Version}"" />
  <Date value=""{DateTime.Now:yyyy-MM-dd HH:mm:ss}"" />
  <Description value="""" />
</AutoDeploy>
";
        File.WriteAllText(Path.Combine(opts.OutputDir, "AutoDeployConfig.xml"), xml, new UTF8Encoding(false));

        var json = $@"{{
  ""{opts.Version}"": {{
    ""date"": ""{DateTime.Now:yyyy-MM-dd}"",
    ""changes"": [
      ""初始版本""
    ]
  }}
}}
";
        File.WriteAllText(Path.Combine(opts.OutputDir, "VersionsInfo.json"), json, new UTF8Encoding(false));
    }

    /// <summary>生成 .gitlab-ci.yml（无 BOM UTF-8）。</summary>
    private static void WriteGitLabCi(BuildOptions opts, bool includeFtp)
    {
        var ftpBase = BuildFtpBase(opts);
        var proj = opts.ProjectCode;
        var cfg = opts.Template.Configuration;
        var tfm = opts.Template.TargetFramework;

        var sb = new StringBuilder();
        sb.AppendLine("variables:");
        sb.AppendLine("  GIT_DEPTH: \"0\"");
        sb.AppendLine();
        sb.AppendLine("deploy:");
        sb.AppendLine("  tags:");
        sb.AppendLine("    - windows");
        sb.AppendLine("  script:");

        if (includeFtp)
        {
            // [1] 从 FTP 获取版本号 +1（参考 G:\CommonDebugTool\upgrade；递增最后一段，兼容 3/4 段版本号）
            sb.AppendLine("    - echo \"=== [1] Version ===\"");
            sb.AppendLine($"    - 'if([string]::IsNullOrWhiteSpace($env:TESTRIG_FTP_USER)-or[string]::IsNullOrWhiteSpace($env:TESTRIG_FTP_PASSWORD)){{throw \"Missing protected CI variables TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD\"}};$cred=[string]::Concat($env:TESTRIG_FTP_USER,[char]58,$env:TESTRIG_FTP_PASSWORD);$ftp=\"{ftpBase}\";curl.exe -s -u $cred \"$ftp/AutoDeployConfig.xml\" -o _v.xml 2>$null;$global:LASTEXITCODE=0;if((Test-Path _v.xml) -and ((gc _v.xml -Raw) -match \"<Version\")){{[xml]$xml=gc _v.xml;$old=$xml.AutoDeploy.Version.value}}else{{[xml]$xml=gc AutoDeployConfig.xml;$old=$xml.AutoDeploy.Version.value}};$p=$old.Split(\".\");$p[$p.Length-1]=[string]([int]$p[$p.Length-1]+1);$NEW=$p -join \".\";Write-Host \"$old -> $NEW\";\"NEW_VERSION=$NEW\"|Out-File _vars.env -Encoding ascii'");

            // [2] 更新 AutoDeployConfig.xml
            sb.AppendLine("    - echo \"=== [2] Update XML ===\"");
            sb.AppendLine("    - '$nv=(gc _vars.env|sls \"NEW_VERSION=\").ToString().Split(\"=\")[1];[xml]$xml=gc AutoDeployConfig.xml;$xml.AutoDeploy.Version.value=$nv;$xml.AutoDeploy.Date.value=(Get-Date).ToString(\"yyyy-MM-dd HH:mm:ss\");$xml.Save(\"AutoDeployConfig.xml\");Write-Host \"-> $nv\"'");

            // [2.5] 更新 VersionsInfo.json
            sb.AppendLine("    - echo \"=== [2.5] Update VersionsInfo.json ===\"");
            sb.AppendLine("    - powershell -ExecutionPolicy Bypass -File upgrade/ci_update_versions.ps1");
        }

        // [3] 编译
        sb.AppendLine("    - echo \"=== [3] Build ===\"");
        sb.AppendLine($"    - dotnet build {proj}.sln -c {cfg}");

        if (includeFtp)
        {
            // [4] 打包 ZIP
            sb.AppendLine("    - echo \"=== [4] ZIP ===\"");
            sb.AppendLine($"    - '$nv=(gc _vars.env|sls \"NEW_VERSION=\").ToString().Split(\"=\")[1];$pub=\"src/08.App/{opts.MainProjectName}/bin/{cfg}/{tfm}\";Compress-Archive $pub\\* {proj}-v$nv.zip -Force;Write-Host \"ZIP: $((ls {proj}-v$nv.zip).Length) bytes\"'");

            // [5] 上传 FTP 三件套
            sb.AppendLine("    - echo \"=== [5] Upload FTP ===\"");
            sb.AppendLine($"    - '$cred=[string]::Concat($env:TESTRIG_FTP_USER,[char]58,$env:TESTRIG_FTP_PASSWORD);$ftp=\"{ftpBase}\";$nv=(gc _vars.env|sls \"NEW_VERSION=\").ToString().Split(\"=\")[1];curl.exe -s -T AutoDeployConfig.xml -u $cred \"$ftp/AutoDeployConfig.xml\" 2>$null;Write-Host \"XML: $LASTEXITCODE\";$global:LASTEXITCODE=0;curl.exe -s -T {proj}-v$nv.zip -u $cred \"$ftp/{proj}-v$nv.zip\" 2>$null;Write-Host \"ZIP: $LASTEXITCODE\";$global:LASTEXITCODE=0;curl.exe -s -T VersionsInfo.json -u $cred \"$ftp/VersionsInfo.json\" 2>$null;Write-Host \"VJ: $LASTEXITCODE\";$global:LASTEXITCODE=0'");
        }

        // [6] Git tag（仅在含 FTP 版本管理时打 tag，避免引用不存在的 _vars.env）
        if (includeFtp)
        {
            sb.AppendLine("    - echo \"=== [6] Git tag ===\"");
            sb.AppendLine("    - '$nv=(gc _vars.env|sls \"NEW_VERSION=\").ToString().Split(\"=\")[1];git tag \"v$nv\";git push origin \"v$nv\";Write-Host \"Tag v$nv\"'");
        }
        sb.AppendLine("    - echo \"=== Done ===\"");

        File.WriteAllText(Path.Combine(opts.OutputDir, ".gitlab-ci.yml"), sb.ToString(), new UTF8Encoding(false));
    }

    /// <summary>生成 upgrade/ci_update_versions.ps1（GitLab CI 步骤 [2.5]，必须 UTF-8 with BOM）。</summary>
    private static void WriteCiUpdateVersionsPs1(BuildOptions opts)
    {
        var ftpBase = BuildFtpBase(opts);

        var content = $@"# ============================================================
# 更新 VersionsInfo.json（CI 步骤 [2.5] 专用）—— 参考 G:\CommonDebugTool\upgrade\ci_update_versions.ps1
# 逻辑：读取 _vars.env 新版本号 -> git log 最近 tag 区间生成 changes
#       -> 以 FTP 现有 VersionsInfo.json 为基线（失败回退本地）合并去重 -> 写回
# 注意：本文件必须保存为 UTF-8 with BOM（PowerShell 5.1 需要）
# ============================================================
$ErrorActionPreference = ""Stop""
$projectRoot = if ($PSScriptRoot) {{ Split-Path $PSScriptRoot -Parent }} else {{ (Get-Location).Path }}
$logPath = Join-Path $projectRoot ""ci_update_versions.log""

function Write-Log($msg) {{
    $line = ""[{0}] {{1}}"" -f (Get-Date).ToString(""yyyy-MM-dd HH:mm:ss""), $msg
    Write-Host $line
    try {{ Add-Content -Path $logPath -Value $line -Encoding UTF8 }} catch {{ }}
}}

# 关键：让 PowerShell 按 UTF-8 解码 git 等原生程序输出（默认 GBK 会导致中文乱码、换行错乱）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

try {{
    $nv = (Get-Content (Join-Path $projectRoot ""_vars.env"") | Select-String ""NEW_VERSION="").ToString().Split(""="")[1]
    Write-Log ""new version: $nv""

    # 1. git log 最近 tag 区间生成 changes
    $lastTag = git tag --sort=-v:refname | Select-Object -First 1
    if ($lastTag) {{ $commits = git log ""${{lastTag}}..HEAD"" --pretty=format:""%s"" }}
    else {{ $commits = git log --pretty=format:""%s"" }}
    $changes = @(@($commits) -split ""`n"" | ForEach-Object {{ [string]$_ }} | Where-Object {{ $_.Trim() -ne """" }})
    if ($changes.Count -eq 0) {{ $changes = @(""日常更新"") }}
    Write-Log ""lastTag=$lastTag changes=$($changes.Count)""

    # 2. FTP 基线（优先），失败回退本地
    $ftpUser = $env:TESTRIG_FTP_USER
    $ftpPassword = $env:TESTRIG_FTP_PASSWORD
    if ([string]::IsNullOrWhiteSpace($ftpUser) -or [string]::IsNullOrWhiteSpace($ftpPassword)) {{
        throw ""缺少环境变量 TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD""
    }}
    $cred = $ftpUser + "":"" + $ftpPassword
    $ftp = ""{ftpBase}""
    curl.exe -s -u $cred ""$ftp/VersionsInfo.json"" -o (Join-Path $projectRoot ""_vi.json"") 2>$null
    $global:LASTEXITCODE = 0
    $viPath = Join-Path $projectRoot ""_vi.json""
    if ((Test-Path $viPath) -and ((Get-Content $viPath -Raw).Trim().Length -gt 2)) {{
        $vo = [System.IO.File]::ReadAllText($viPath, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
        Write-Log ""baseline: FTP""
    }} else {{
        $vo = [System.IO.File]::ReadAllText((Join-Path $projectRoot ""VersionsInfo.json""), [System.Text.Encoding]::UTF8) | ConvertFrom-Json
        Write-Log ""baseline: local""
    }}

    # 3. 去重添加新版本条目
    if (-not ($vo.PSObject.Properties.Name -contains $nv)) {{
        $entry = [PSCustomObject]@{{ date = (Get-Date).ToString(""yyyy-MM-dd""); changes = $changes }}
        $vo | Add-Member -NotePropertyName $nv -NotePropertyValue $entry -Force
        Write-Log ""VersionsInfo.json +v$nv ($($changes.Count) changes)""
    }} else {{
        Write-Log ""VersionsInfo.json v$nv already exists, skip""
    }}

    $json = $vo | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText((Join-Path $projectRoot ""VersionsInfo.json""), $json, (New-Object System.Text.UTF8Encoding($true)))
    Write-Log ""VersionsInfo.json written""

    # 4. 同步更新 AutoDeployConfig.xml 的 Description
    $xmlPath = Join-Path $projectRoot ""AutoDeployConfig.xml""
    if ((Test-Path $xmlPath) -and $changes.Count -gt 0) {{
        $deployXml = New-Object System.Xml.XmlDocument
        $deployXml.Load($xmlPath)
        $descNode = $deployXml.SelectSingleNode(""//Description"")
        if ($descNode) {{
            $descNode.SetAttribute(""value"", ([string]$changes[0]).Trim())
            $deployXml.Save($xmlPath)
            Write-Log ""AutoDeployConfig.xml Description -> $($changes[0])""
        }}
    }}

    Write-Log ""done""
}} catch {{
    $err = ""ERROR: $($_.Exception.Message) | stack: $($_.ScriptStackTrace)""
    Write-Log $err
    exit 1
}}
";
        WriteFileUtf8Bom(Path.Combine(opts.OutputDir, "upgrade", "ci_update_versions.ps1"), content);
    }

    /// <summary>生成 upgrade/publish.ps1（GitLab+FTP 手动发布脚本，参考 G:\CommonDebugTool\upgrade\publish.ps1）。</summary>
    private static void WritePublishPs1(BuildOptions opts)
    {
        var ftpBase = BuildFtpBase(opts);
        var proj = opts.ProjectCode;
        var cfg = opts.Template.Configuration;
        var tfm = opts.Template.TargetFramework;

        var content = $@"# ============================================================
# {proj} 发布脚本（参考 G:\CommonDebugTool\upgrade\publish.ps1）
# 用法：在项目根目录执行 .\upgrade\publish.ps1
# ============================================================
$ErrorActionPreference = ""Stop""

# 项目根目录 = 本脚本所在目录（upgrade/）的上一级
$ProjectRoot = if ($PSScriptRoot) {{ Split-Path $PSScriptRoot -Parent }} else {{ (Get-Location).Path }}
Set-Location $ProjectRoot

$FTP_BASE = ""{ftpBase}""
$FTP_USER = $env:TESTRIG_FTP_USER
$FTP_PASS = $env:TESTRIG_FTP_PASSWORD
if ([string]::IsNullOrWhiteSpace($FTP_USER) -or [string]::IsNullOrWhiteSpace($FTP_PASS)) {{
    throw ""缺少环境变量 TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD""
}}
$FTP_CRED = ""${{FTP_USER}}:${{FTP_PASS}}""

Write-Host ""========================================"" -ForegroundColor Cyan
Write-Host ""  {proj} 自动发布脚本"" -ForegroundColor Cyan
Write-Host ""========================================"" -ForegroundColor Cyan

# 1. 从 FTP 获取版本号（失败回退本地 AutoDeployConfig.xml）
Write-Host ""`n[1/7] 获取版本号..."" -ForegroundColor Yellow
curl.exe -s --connect-timeout 10 -u $FTP_CRED ""$FTP_BASE/AutoDeployConfig.xml"" -o ftp_version.xml 2>$null
$global:LASTEXITCODE = 0
if ((Test-Path ftp_version.xml) -and ((Get-Content ftp_version.xml -Raw) -match ""<Version"")) {{
    [xml]$xml = Get-Content ftp_version.xml
}} else {{
    [xml]$xml = Get-Content AutoDeployConfig.xml
}}
$oldVer = $xml.AutoDeploy.Version.value
$parts = $oldVer.Split(""."")
$parts[$parts.Length - 1] = [string]([int]$parts[$parts.Length - 1] + 1)
$newVer = $parts -join "".""
Write-Host ""  版本: $oldVer -> $newVer"" -ForegroundColor Green

# 2. 更新 AutoDeployConfig.xml
Write-Host ""`n[2/7] 更新 AutoDeployConfig.xml..."" -ForegroundColor Yellow
$xml.AutoDeploy.Version.value = $newVer
$xml.AutoDeploy.Date.value = (Get-Date).ToString(""yyyy-MM-dd HH:mm:ss"")
$xml.Save((Join-Path $ProjectRoot ""AutoDeployConfig.xml""))
Write-Host ""  已完成"" -ForegroundColor Green

# 3. 编译
Write-Host ""`n[3/7] dotnet build..."" -ForegroundColor Yellow
dotnet build ""$ProjectRoot\{proj}.sln"" -c {cfg}
if ($LASTEXITCODE -ne 0) {{ throw ""Build failed"" }}
Write-Host ""  编译完成"" -ForegroundColor Green

# 4. 打包 ZIP
Write-Host ""`n[4/7] 打包 {proj}-v$newVer.zip..."" -ForegroundColor Yellow
$publishDir = ""$ProjectRoot\src\08.App\{opts.MainProjectName}\bin\{cfg}\{tfm}""
Compress-Archive -Path ""$publishDir\*"" -DestinationPath ""$ProjectRoot\{proj}-v$newVer.zip"" -Force
Write-Host ""  ZIP 大小: $((Get-Item ""$ProjectRoot\{proj}-v$newVer.zip"").Length) 字节"" -ForegroundColor Green

# 5. 更新 VersionsInfo.json
Write-Host ""`n[5/7] 更新 VersionsInfo.json..."" -ForegroundColor Yellow
$lastTag = git -C $ProjectRoot tag --sort=-v:refname | Select-Object -First 1
if ($lastTag) {{ $commits = git -C $ProjectRoot log ""${{lastTag}}..HEAD"" --pretty=format:""%s"" }}
else {{ $commits = git -C $ProjectRoot log --pretty=format:""%s"" }}
$changes = @($commits -split ""`n"" | Where-Object {{ $_.Trim() -ne """" }})
if ($changes.Count -eq 0) {{ $changes = @(""日常更新"") }}
$vo = Get-Content (Join-Path $ProjectRoot ""VersionsInfo.json"") -Raw | ConvertFrom-Json
$entry = [PSCustomObject]@{{ date = (Get-Date).ToString(""yyyy-MM-dd""); changes = $changes }}
$vo | Add-Member -NotePropertyName $newVer -NotePropertyValue $entry -Force
$vo | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $ProjectRoot ""VersionsInfo.json"") -Encoding UTF8
Write-Host ""  新增 $($changes.Count) 条变更"" -ForegroundColor Green

# 6. 上传 FTP 三件套
Write-Host ""`n[6/7] 上传到 FTP..."" -ForegroundColor Yellow
curl.exe -s -T (Join-Path $ProjectRoot ""AutoDeployConfig.xml"") -u $FTP_CRED ""$FTP_BASE/AutoDeployConfig.xml""
Write-Host ""  + AutoDeployConfig.xml""
curl.exe -s -T (Join-Path $ProjectRoot ""{proj}-v$newVer.zip"") -u $FTP_CRED ""$FTP_BASE/{proj}-v$newVer.zip""
Write-Host ""  + {proj}-v$newVer.zip""
curl.exe -s -T (Join-Path $ProjectRoot ""VersionsInfo.json"") -u $FTP_CRED ""$FTP_BASE/VersionsInfo.json""
Write-Host ""  + VersionsInfo.json""
Write-Host ""  上传完成"" -ForegroundColor Green

# 7. 清理
Write-Host ""`n[7/7] 清理临时文件..."" -ForegroundColor Yellow
Remove-Item (Join-Path $ProjectRoot ""ftp_version.xml"") -ErrorAction SilentlyContinue
Remove-Item (Join-Path $ProjectRoot ""{proj}-v$newVer.zip"") -ErrorAction SilentlyContinue

Write-Host ""`n=========================================="" -ForegroundColor Cyan
Write-Host ""  发布完成! 版本: v$newVer"" -ForegroundColor Cyan
Write-Host ""  别忘了 git commit + tag + push !"" -ForegroundColor Cyan
Write-Host ""    git add AutoDeployConfig.xml VersionsInfo.json"" -ForegroundColor White
Write-Host ""    git commit -m 'release: v$newVer'"" -ForegroundColor White
Write-Host ""    git tag v$newVer"" -ForegroundColor White
Write-Host ""    git push origin main --tags"" -ForegroundColor White
Write-Host ""=========================================="" -ForegroundColor Cyan
";
        WriteFileUtf8Bom(Path.Combine(opts.OutputDir, "upgrade", "publish.ps1"), content);
    }

    /// <summary>生成 upgrade/local_test_ci.ps1（本地 CI 模拟，参考 G:\CommonDebugTool\upgrade\local_test_ci.ps1）。</summary>
    private static void WriteLocalTestCiPs1(BuildOptions opts)
    {
        var ftpBase = BuildFtpBase(opts);
        var proj = opts.ProjectCode;
        var cfg = opts.Template.Configuration;
        var tfm = opts.Template.TargetFramework;

        var content = $@"# ============================================================
# 本地 CI 模拟脚本（参考 G:\CommonDebugTool\upgrade\local_test_ci.ps1）
# .\upgrade\local_test_ci.ps1          # 完整运行
# .\upgrade\local_test_ci.ps1 -DryRun  # 试运行（不执行上传/tag）
# ============================================================
param([switch]$DryRun)
$ErrorActionPreference = ""Stop""
$ProjectRoot = if ($PSScriptRoot) {{ Split-Path $PSScriptRoot -Parent }} else {{ (Get-Location).Path }}
Set-Location $ProjectRoot

$FTP_BASE = ""{ftpBase}""
$FTP_USER = $env:TESTRIG_FTP_USER
$FTP_PASS = $env:TESTRIG_FTP_PASSWORD
if ([string]::IsNullOrWhiteSpace($FTP_USER) -or [string]::IsNullOrWhiteSpace($FTP_PASS)) {{
    throw ""缺少环境变量 TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD""
}}
$cred = ""${{FTP_USER}}:${{FTP_PASS}}""
$PROJ = ""{proj}""

Write-Host ""========================================"" -ForegroundColor Cyan
Write-Host ""  $PROJ 本地 CI 模拟"" -ForegroundColor Cyan
if ($DryRun) {{ Write-Host ""  [DryRun - skip upload/tag]"" -ForegroundColor Yellow }}
Write-Host ""========================================"" -ForegroundColor Cyan

# [1] 从 FTP 获取版本号 +1（失败回退本地）
Write-Host ""[1/8] Version from FTP..."" -ForegroundColor Yellow
curl.exe -s --connect-timeout 10 -u $cred ""$FTP_BASE/AutoDeployConfig.xml"" -o ftp_version.xml 2>$null
$global:LASTEXITCODE = 0
if ((Test-Path ftp_version.xml) -and ((Get-Content ftp_version.xml -Raw) -match ""<Version"")) {{
    [xml]$xml = Get-Content ftp_version.xml
}} else {{
    [xml]$xml = Get-Content AutoDeployConfig.xml
}}
$oldVer = $xml.AutoDeploy.Version.value
$parts = $oldVer.Split(""."")
$parts[$parts.Length - 1] = [string]([int]$parts[$parts.Length - 1] + 1)
$NEW_VERSION = $parts -join "".""
Write-Host ""Version: $oldVer -> $NEW_VERSION"" -ForegroundColor Green
""NEW_VERSION=$NEW_VERSION"" | Out-File -FilePath vars.env

# [2] 更新 AutoDeployConfig.xml
Write-Host ""[2/8] Update AutoDeployConfig.xml..."" -ForegroundColor Yellow
$newVer = (Get-Content vars.env | Select-String ""NEW_VERSION="").ToString().Split(""="")[1]
[xml]$xml = Get-Content AutoDeployConfig.xml
$xml.AutoDeploy.Version.value = $newVer
$xml.AutoDeploy.Date.value = (Get-Date).ToString(""yyyy-MM-dd HH:mm:ss"")
$xml.Save(""$PWD\AutoDeployConfig.xml"")
Write-Host ""Updated -> $newVer"" -ForegroundColor Green

# [3] dotnet build
Write-Host ""[3/8] dotnet build..."" -ForegroundColor Yellow
dotnet build ""$PWD\$PROJ.sln"" -c {cfg}
if ($LASTEXITCODE -ne 0) {{ throw ""Build failed"" }}
Write-Host ""Build OK"" -ForegroundColor Green

# [4] 打包 ZIP
Write-Host ""[4/8] Create ZIP..."" -ForegroundColor Yellow
$pub = ""$PWD\src\08.App\{opts.MainProjectName}\bin\{cfg}\{tfm}""
Compress-Archive -Path ""$pub\*"" -DestinationPath ""$PROJ-v$newVer.zip"" -Force
Write-Host ""ZIP: $((Get-Item ""$PROJ-v$newVer.zip"").Length) bytes"" -ForegroundColor Green

# [5] 更新 VersionsInfo.json
Write-Host ""[5/8] Update VersionsInfo.json..."" -ForegroundColor Yellow
$vo = Get-Content VersionsInfo.json -Raw | ConvertFrom-Json
$entry = [PSCustomObject]@{{ date = (Get-Date).ToString(""yyyy-MM-dd""); changes = @(""日常更新"") }}
$vo | Add-Member -NotePropertyName $newVer -NotePropertyValue $entry -Force
$vo | ConvertTo-Json -Depth 10 | Set-Content VersionsInfo.json -Encoding UTF8
Write-Host ""Added version $newVer"" -ForegroundColor Green

# [6] 上传 FTP（DryRun 跳过）
if ($DryRun) {{
    Write-Host ""[6/8] Upload FTP [SKIP - DryRun]"" -ForegroundColor Yellow
}} else {{
    Write-Host ""[6/8] Upload to FTP..."" -ForegroundColor Yellow
    curl.exe -s --connect-timeout 10 -T AutoDeployConfig.xml -u $cred ""$FTP_BASE/AutoDeployConfig.xml"" 2>$null
    Write-Host ""  AutoDeployConfig.xml: $LASTEXITCODE""; $global:LASTEXITCODE = 0
    curl.exe -s --connect-timeout 30 -T ""$PROJ-v$newVer.zip"" -u $cred ""$FTP_BASE/$PROJ-v$newVer.zip"" 2>$null
    Write-Host ""  ZIP: $LASTEXITCODE""; $global:LASTEXITCODE = 0
    curl.exe -s --connect-timeout 10 -T VersionsInfo.json -u $cred ""$FTP_BASE/VersionsInfo.json"" 2>$null
    Write-Host ""  VersionsInfo.json: $LASTEXITCODE""; $global:LASTEXITCODE = 0
    Write-Host ""Upload done"" -ForegroundColor Green
}}

# [7] Git tag（DryRun 跳过）
if ($DryRun) {{
    Write-Host ""[7/8] Git tag [SKIP - DryRun]"" -ForegroundColor Yellow
}} else {{
    Write-Host ""[7/8] Git tag..."" -ForegroundColor Yellow
    git tag ""v$newVer""
    git push origin ""v$newVer""
    Write-Host ""Tag v$newVer pushed"" -ForegroundColor Green
}}

# [8] 清理
Write-Host ""[8/8] Cleanup..."" -ForegroundColor Yellow
Remove-Item ftp_version.xml, vars.env -ErrorAction SilentlyContinue
Write-Host ""Done"" -ForegroundColor Green
";
        WriteFileUtf8Bom(Path.Combine(opts.OutputDir, "upgrade", "local_test_ci.ps1"), content);
    }

    /// <summary>仅 FTP：自写实现 publish_ftp.ps1（编译 → 打包 → FTP 上传三件套）。</summary>
    private static void WriteFtpOnlyPublishPs1(BuildOptions opts)
    {
        var ftpBase = BuildFtpBase(opts);
        var proj = opts.ProjectCode;
        var cfg = opts.Template.Configuration;
        var tfm = opts.Template.TargetFramework;

        var content = $@"# ============================================================
# {proj} FTP 发布脚本（TestRig CLI 自写实现 —— 仅上传 FTP 服务器）
# 用法：在项目根目录执行 .\publish_ftp.ps1
# 流程：获取版本号+1 -> 更新 AutoDeployConfig.xml -> dotnet build
#       -> 打包 ZIP -> 上传 FTP 三件套 -> 清理
# ============================================================
$ErrorActionPreference = ""Stop""

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

$FTP_BASE = ""{ftpBase}""
$FTP_USER = $env:TESTRIG_FTP_USER
$FTP_PASS = $env:TESTRIG_FTP_PASSWORD
if ([string]::IsNullOrWhiteSpace($FTP_USER) -or [string]::IsNullOrWhiteSpace($FTP_PASS)) {{
    throw ""缺少环境变量 TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD""
}}
$FTP_CRED = ""${{FTP_USER}}:${{FTP_PASS}}""
$PROJ = ""{proj}""

Write-Host ""========================================"" -ForegroundColor Cyan
Write-Host ""  $PROJ FTP 自动发布"" -ForegroundColor Cyan
Write-Host ""  FTP: $FTP_BASE"" -ForegroundColor Cyan
Write-Host ""========================================"" -ForegroundColor Cyan

# 1. 获取版本号（优先 FTP，失败回退本地 AutoDeployConfig.xml）
Write-Host ""`n[1/6] 获取版本号..."" -ForegroundColor Yellow
curl.exe -s --connect-timeout 10 -u $FTP_CRED ""$FTP_BASE/AutoDeployConfig.xml"" -o ftp_version.xml 2>$null
$global:LASTEXITCODE = 0
if ((Test-Path ftp_version.xml) -and ((Get-Content ftp_version.xml -Raw) -match ""<Version"")) {{
    [xml]$xml = Get-Content ftp_version.xml
    Write-Host ""  版本来源: FTP"" -ForegroundColor Green
}} else {{
    [xml]$xml = Get-Content AutoDeployConfig.xml
    Write-Host ""  版本来源: 本地（FTP 不可达）"" -ForegroundColor Yellow
}}
$oldVer = $xml.AutoDeploy.Version.value
$parts = $oldVer.Split(""."")
$parts[$parts.Length - 1] = [string]([int]$parts[$parts.Length - 1] + 1)
$newVer = $parts -join "".""
Write-Host ""  版本: $oldVer -> $newVer"" -ForegroundColor Green

# 2. 更新 AutoDeployConfig.xml（版本 + 日期）
Write-Host ""`n[2/6] 更新 AutoDeployConfig.xml..."" -ForegroundColor Yellow
$xml.AutoDeploy.Version.value = $newVer
$xml.AutoDeploy.Date.value = (Get-Date).ToString(""yyyy-MM-dd HH:mm:ss"")
$xml.Save((Join-Path $ProjectRoot ""AutoDeployConfig.xml""))
Write-Host ""  已完成"" -ForegroundColor Green

# 3. 编译
Write-Host ""`n[3/6] dotnet build..."" -ForegroundColor Yellow
dotnet build ""$ProjectRoot\$PROJ.sln"" -c {cfg}
if ($LASTEXITCODE -ne 0) {{ throw ""Build failed"" }}
Write-Host ""  编译完成"" -ForegroundColor Green

# 4. 打包 ZIP（包含 AutoDeployConfig.xml + VersionsInfo.json，确保解压即得版本信息）
Write-Host ""`n[4/6] 打包 {proj}-v$newVer.zip..."" -ForegroundColor Yellow
$publishDir = ""$ProjectRoot\src\08.App\{opts.MainProjectName}\bin\{cfg}\{tfm}""
if (-not (Test-Path $publishDir)) {{ throw ""未找到编译产物: $publishDir"" }}
Copy-Item (Join-Path $ProjectRoot ""AutoDeployConfig.xml"") ""$publishDir\AutoDeployConfig.xml"" -Force
Copy-Item (Join-Path $ProjectRoot ""VersionsInfo.json"") ""$publishDir\VersionsInfo.json"" -Force
$zipPath = ""$ProjectRoot\{proj}-v$newVer.zip""
Compress-Archive -Path ""$publishDir\*"" -DestinationPath $zipPath -Force
Write-Host ""  ZIP 大小: $((Get-Item $zipPath).Length) 字节"" -ForegroundColor Green

# 5. 上传 FTP 三件套
Write-Host ""`n[5/6] 上传到 FTP..."" -ForegroundColor Yellow
curl.exe -s -T (Join-Path $ProjectRoot ""AutoDeployConfig.xml"") -u $FTP_CRED ""$FTP_BASE/AutoDeployConfig.xml""
Write-Host ""  + AutoDeployConfig.xml ($LASTEXITCODE)""; $global:LASTEXITCODE = 0
curl.exe -s -T $zipPath -u $FTP_CRED ""$FTP_BASE/$PROJ-v$newVer.zip""
Write-Host ""  + {proj}-v$newVer.zip ($LASTEXITCODE)""; $global:LASTEXITCODE = 0
curl.exe -s -T (Join-Path $ProjectRoot ""VersionsInfo.json"") -u $FTP_CRED ""$FTP_BASE/VersionsInfo.json""
Write-Host ""  + VersionsInfo.json ($LASTEXITCODE)""; $global:LASTEXITCODE = 0
Write-Host ""  上传完成"" -ForegroundColor Green

# 6. 清理
Write-Host ""`n[6/6] 清理临时文件..."" -ForegroundColor Yellow
Remove-Item (Join-Path $ProjectRoot ""ftp_version.xml"") -ErrorAction SilentlyContinue

Write-Host ""`n========================================"" -ForegroundColor Cyan
Write-Host ""  发布完成! 版本: v$newVer"" -ForegroundColor Cyan
Write-Host ""  产物: {proj}-v$newVer.zip"" -ForegroundColor Cyan
Write-Host ""========================================"" -ForegroundColor Cyan
";
        WriteFileUtf8Bom(Path.Combine(opts.OutputDir, "publish_ftp.ps1"), content);
    }

    /// <summary>生成 push_gitlab.ps1（初始化 git 仓库 + 添加 remote + 推送到 GitLab）。</summary>
    private static void WritePushGitlabPs1(BuildOptions opts)
    {
        var content = $@"# ============================================================
# 推送到 GitLab（初始化 git 仓库并推送代码）
# 用法：在项目根目录执行 .\push_gitlab.ps1
# 前置：已在 GitLab 创建空仓库，地址由向导输入
#
# FTP 凭据不写入仓库。启用 FTP 时，请在 GitLab 中配置受保护且掩码的
# TESTRIG_FTP_USER / TESTRIG_FTP_PASSWORD CI/CD Variables。
# ============================================================
$ErrorActionPreference = ""Stop""
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ProjectRoot

$GitLabUrl = ""{opts.GitLabRepoUrl}""

if (-not (Test-Path .git)) {{
    Write-Host ""[1/4] git init..."" -ForegroundColor Yellow
    git init
}}

# 若 remote 已存在则更新，否则添加
$remoteExists = git remote | Select-String -Quiet ""origin""
if ($remoteExists) {{
    git remote set-url origin $GitLabUrl
}} else {{
    git remote add origin $GitLabUrl
}}
Write-Host ""[2/4] remote: $GitLabUrl"" -ForegroundColor Green

Write-Host ""[3/4] git add + commit..."" -ForegroundColor Yellow
git add .
if (-not (git diff --cached --quiet)) {{
    git commit -m ""initial: {opts.ProjectCode} 脚手架生成""
}} else {{
    Write-Host ""  无新增改动，跳过 commit"" -ForegroundColor DarkGray
}}

Write-Host ""[4/4] git push..."" -ForegroundColor Yellow
$branch = git branch --show-current
if (-not $branch) {{ $branch = ""main"" }}
git push -u origin $branch
Write-Host ""推送完成: origin/$branch"" -ForegroundColor Green
";
        // PowerShell 5.1 解析含中文脚本需要 UTF-8 with BOM，否则注释/消息乱码
        WriteFileUtf8Bom(Path.Combine(opts.OutputDir, "push_gitlab.ps1"), content);
    }

    /// <summary>生成 .gitignore（排除编译产物、构建目录与 CI 临时文件；保留 .gitlab-ci.yml / upgrade/ / 版本文件以便 CI 使用）。</summary>
    private static void WriteGitIgnore(BuildOptions opts)
    {
        var content = @"# ===== 编译产物 / 构建目录（推送 GitLab 时忽略）=====
[Bb]in/
[Oo]bj/
publish/
_publish/
*.zip
*.log
.idea/
.vs/
.vscode/
.reasonix/
.git/

# ===== CI / 发布脚本运行时产生的临时文件 =====
_v.xml
_vars.env
_vi.json
vars.env
changes.json
ftp_version.xml
ftp_v.json
ftp_versions.json
";
        File.WriteAllText(Path.Combine(opts.OutputDir, ".gitignore"), content, new UTF8Encoding(false));
    }

    /// <summary>拼装 FTP 基础 URL（host + 远程目录）。</summary>
    private static string BuildFtpBase(BuildOptions opts)
    {
        var host = opts.FtpHost.Trim().TrimEnd('/');
        var dir = opts.FtpRemoteDir?.Trim().Trim('/') ?? "";
        return string.IsNullOrEmpty(dir) ? host : $"{host}/{dir}";
    }

    /// <summary>以 UTF-8 with BOM 写文件（PowerShell 5.1 解析中文需要）。</summary>
    private static void WriteFileUtf8Bom(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content, new UTF8Encoding(true));
    }
}
