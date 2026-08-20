using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 安装包打包器 —— 检测 Inno Setup (ISCC.exe)、生成 .iss 脚本、生成 package.ps1。
/// 参考实现：E:\ExeBuilder\Services\BuildService.cs（GenerateInnoScript）。
/// </summary>
public static class InstallerPackager
{
    /// <summary>检测 Inno Setup 编译器 (ISCC.exe) 路径。</summary>
    public static string? FindInnoSetup()
    {
        if (!OperatingSystem.IsWindows()) return null;

        try
        {
            // 1. PATH
            var pathCmd = GetCommandPath("iscc.exe") ?? GetCommandPath("ISCC.exe");
            if (pathCmd != null) return pathCmd;

            // 2. 注册表 —— 同时查 32/64 位视图：Inno Setup 是 32 位程序，其卸载项注册在
            //    WOW6432Node 下（32 位视图），64 位进程默认视图读不到，会误报"未安装"。
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                {
                    try
                    {
                        using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                        using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup 6_is1");
                        if (key?.GetValue("InstallLocation") is string loc && !string.IsNullOrEmpty(loc))
                        {
                            var iscc = Path.Combine(loc, "ISCC.exe");
                            if (File.Exists(iscc)) return iscc;
                        }
                    }
                    catch { }
                }
            }

            // 3. 所有盘符的 Program Files / Program Files (x86) —— Inno Setup 可能装在非系统盘
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                var root = drive.RootDirectory.FullName;
                foreach (var pf in new[] { Path.Combine(root, "Program Files"), Path.Combine(root, "Program Files (x86)") })
                {
                    if (!Directory.Exists(pf)) continue;
                    var iscc = Path.Combine(pf, "Inno Setup 6", "ISCC.exe");
                    if (File.Exists(iscc)) return iscc;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>使用 where 命令查找 PATH 中的可执行文件。</summary>
    private static string? GetCommandPath(string name)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = name,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return null;
            if (!proc.WaitForExit(3000))
            {
                try { proc.Kill(); } catch { }
                return null;
            }
            var output = proc.StandardOutput.ReadToEnd();
            var firstLine = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (!string.IsNullOrEmpty(firstLine) && File.Exists(firstLine)) return firstLine;
        }
        catch { }
        return null;
    }

    /// <summary>生成 .iss 安装脚本内容。</summary>
    public static string GenerateInnoScript(BuildOptions opts)
    {
        var sb = new StringBuilder();
        var publishDir = opts.PublishDir;
        var installerDir = Path.Combine(opts.OutputDir, "installer");

        sb.AppendLine("; 由 TestRig CLI 自动生成");
        sb.AppendLine($"; 产品代号: {opts.ProductCode}  生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine("[Setup]");
        sb.AppendLine($"AppName={opts.ProductCode}");
        sb.AppendLine("AppPublisher=TestRig CLI");
        sb.AppendLine($"DefaultDirName={{autopf}}\\{opts.ProductCode}");
        sb.AppendLine($"DefaultGroupName={opts.ProductCode}");
        sb.AppendLine($"OutputBaseFilename={opts.ProductCode}-Setup");
        sb.AppendLine($"OutputDir={installerDir}");
        sb.AppendLine("Compression=lzma2");
        sb.AppendLine("SolidCompression=yes");
        sb.AppendLine("ArchitecturesInstallIn64BitMode=x64");
        sb.AppendLine("DisableProgramGroupPage=yes");
        sb.AppendLine("PrivilegesRequired=lowest");
        sb.AppendLine();
        sb.AppendLine("[Files]");
        sb.AppendLine($"Source: \"{publishDir}\\*\"; DestDir: \"{{app}}\"; Flags: ignoreversion recursesubdirs createallsubdirs");
        sb.AppendLine();
        sb.AppendLine("[Icons]");
        sb.AppendLine($"Name: \"{{group}}\\{opts.ProductCode}\"; Filename: \"{{app}}\\{opts.MainExeFileName}\"");
        sb.AppendLine($"Name: \"{{userdesktop}}\\{opts.ProductCode}\"; Filename: \"{{app}}\\{opts.MainExeFileName}\"; Tasks: desktopicon");
        sb.AppendLine();
        sb.AppendLine("[Tasks]");
        sb.AppendLine("Name: \"desktopicon\"; Description: \"创建桌面快捷方式\"; Flags: unchecked");
        sb.AppendLine();
        sb.AppendLine("[Run]");
        sb.AppendLine($"Filename: \"{{app}}\\{opts.MainExeFileName}\"; Description: \"立即启动\"; Flags: nowait postinstall skipifsilent");

        return sb.ToString();
    }

    /// <summary>生成 package.ps1 脚本内容。</summary>
    public static string GeneratePackagePs1(BuildOptions opts)
    {
        var installerDir = Path.Combine(opts.OutputDir, "installer");
        var issFileName = $"{opts.ProductCode}.iss";
        var installerExe = $"{opts.ProductCode}-Setup.exe";

        var sb = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        sb.AppendLine("# ===== Inno Setup 打包脚本（由 TestRig CLI 自动生成，请勿手动修改）=====");
        sb.AppendLine($"# 产品代号: {opts.ProductCode}");
        sb.AppendLine($"# 生成时间: {timestamp}");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = \"Stop\"");
        sb.AppendLine("$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path");
        sb.AppendLine($"$PublishDir = Join-Path $ProjectRoot \"src\\08.App\\{opts.MainProjectName}\\bin\\{opts.Template.Configuration}\\{opts.Template.TargetFramework}\"");
        sb.AppendLine("$InstallerDir = Join-Path $ProjectRoot \"installer\"");
        sb.AppendLine("$BuildDir = Join-Path $ProjectRoot \"build\"");
        sb.AppendLine();
        sb.AppendLine("Write-Host \"====== 开始打包 ======\" -ForegroundColor Cyan");
        sb.AppendLine();
        sb.AppendLine("# 1. 检测 Inno Setup");
        sb.AppendLine("$iscc = Find-InnoSetup");
        sb.AppendLine("if (-not $iscc) {");
        sb.AppendLine("    Write-Host \"[ERROR] 未找到 Inno Setup (ISCC.exe)\" -ForegroundColor Red");
        sb.AppendLine("    Write-Host \"请从 https://jrsoftware.org/isdl.php 下载安装\" -ForegroundColor Yellow");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine("Write-Host \"[OK] ISCC: $iscc\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# 2. 验证编译产物存在");
        sb.AppendLine($"$mainExe = Join-Path $PublishDir \"{opts.MainExeFileName}\"");
        sb.AppendLine("if (-not (Test-Path $mainExe)) {");
        sb.AppendLine("    Write-Host \"[ERROR] 主程序未找到: $mainExe\" -ForegroundColor Red");
        sb.AppendLine("    Write-Host \"请先执行 dotnet build -c Release\" -ForegroundColor Yellow");
        sb.AppendLine("    exit 1");
        sb.AppendLine("}");
        sb.AppendLine("Write-Host \"[OK] 主程序: $mainExe\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# 3. 创建输出目录");
        sb.AppendLine("if (-not (Test-Path $InstallerDir)) { New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null }");
        sb.AppendLine("if (-not (Test-Path $BuildDir)) { New-Item -ItemType Directory -Path $BuildDir -Force | Out-Null }");
        sb.AppendLine();
        sb.AppendLine("# 4. 生成 .iss 脚本");

        // 在 PS 中嵌入 .iss here-string，需转义 $ 和大括号 {}
        var issContent = GenerateInnoScript(opts);
        // .iss 中的 {app}、{autopf}、{group} 等是 Inno Setup 常量，不会被 PS 解析
        // 但 $ 必须转义为 `$
        var issPsEscape = issContent.Replace("$", "`$");

        sb.AppendLine("$iss = @\"");
        sb.Append(issPsEscape);
        sb.AppendLine("\"@");
        sb.AppendLine($"$issPath = Join-Path $BuildDir \"{issFileName}\"");
        sb.AppendLine("$iss | Out-File $issPath -Encoding UTF8");
        sb.AppendLine("Write-Host \"[OK] ISS 脚本: $issPath\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# 5. 执行 ISCC 编译");
        sb.AppendLine("Write-Host \"[INFO] 正在编译安装包...\" -ForegroundColor Yellow");
        sb.AppendLine("& $iscc \"$issPath\"");
        sb.AppendLine("$exitCode = $LASTEXITCODE");
        sb.AppendLine("if ($exitCode -ne 0) {");
        sb.AppendLine("    Write-Host \"[ERROR] ISCC 编译失败，退出码: $exitCode\" -ForegroundColor Red");
        sb.AppendLine("    exit $exitCode");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"Write-Host \"✓ 打包完成: $InstallerDir\\{installerExe}\" -ForegroundColor Green");
        sb.AppendLine();
        sb.AppendLine("# ===== 辅助函数 =====");
        sb.AppendLine("function Find-InnoSetup {");
        sb.AppendLine("    # 1. PATH");
        sb.AppendLine("    $cmd = Get-Command iscc -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($cmd) { return $cmd.Source }");
        sb.AppendLine("    $cmd = Get-Command ISCC -ErrorAction SilentlyContinue");
        sb.AppendLine("    if ($cmd) { return $cmd.Source }");
        sb.AppendLine("    # 2. 注册表（含 32 位视图 WOW6432Node：Inno Setup 是 32 位程序）");
        sb.AppendLine("    foreach ($hive in @(\"HKLM:\", \"HKCU:\")) {");
        sb.AppendLine("        foreach ($sub in @(\"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Inno Setup 6_is1\", \"SOFTWARE\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Inno Setup 6_is1\")) {");
        sb.AppendLine("            try {");
        sb.AppendLine("                $key = Get-ItemProperty \"$hive\\$sub\" -ErrorAction SilentlyContinue");
        sb.AppendLine("                if ($key.InstallLocation) {");
        sb.AppendLine("                    $iscc = Join-Path $key.InstallLocation \"ISCC.exe\"");
        sb.AppendLine("                    if (Test-Path $iscc) { return $iscc }");
        sb.AppendLine("                }");
        sb.AppendLine("            } catch { }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    # 3. 所有盘符的 Program Files（Inno Setup 可能装在非系统盘）");
        sb.AppendLine("    $drives = Get-PSDrive -PSProvider FileSystem -ErrorAction SilentlyContinue");
        sb.AppendLine("    foreach ($drive in $drives) {");
        sb.AppendLine("        foreach ($pf in @(\"$($drive.Root)Program Files\", \"$($drive.Root)Program Files (x86)\")) {");
        sb.AppendLine("            $iscc = Join-Path $pf \"Inno Setup 6\\ISCC.exe\"");
        sb.AppendLine("            if (Test-Path $iscc) { return $iscc }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("    return $null");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
