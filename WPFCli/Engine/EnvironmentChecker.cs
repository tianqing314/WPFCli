using System.Diagnostics;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 环境检测器 —— 在流水线开始前检测 .NET SDK（必需）、Obfuscar / ISCC（按需，可选）。
/// 缺失必需项时报错退出；缺失可选项时给出安装提示但不阻塞。
/// </summary>
public static class EnvironmentChecker
{
    public class CheckResult
    {
        public bool DotnetAvailable { get; set; }
        public string? DotnetVersion { get; set; }
        public bool SdkVersionSufficient { get; set; }
        public bool ObfuscarAvailable { get; set; }
        public string? ObfuscarPath { get; set; }
        public bool InnoSetupAvailable { get; set; }
        public string? InnoSetupPath { get; set; }
    }

    /// <summary>检测环境，返回结果。</summary>
    public static CheckResult Check(BuildOptions opts)
    {
        var result = new CheckResult();

        // 1. .NET SDK（必需）
        result.DotnetAvailable = CheckDotnet(out var version);
        result.DotnetVersion = version;
        if (!result.DotnetAvailable)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  [✗] 未检测到 .NET SDK");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("      请安装 .NET 8 SDK: https://dotnet.microsoft.com/download");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [✓] .NET SDK: {version}");
            Console.ResetColor();

            // 模板目标框架为 net8.0-windows10.0.19041.0，需 SDK 主版本 >= 8 才能编译
            result.SdkVersionSufficient = CheckSdkMajorAtLeast(8);
            if (!result.SdkVersionSufficient)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  [✗] 未检测到 .NET 8+ SDK（模板目标框架为 net8.0-windows10.0.19041.0）");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("      请安装 .NET 8 SDK: https://dotnet.microsoft.com/download");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("  [✓] 可编译 net8.0-windows 目标（已安装 .NET 8+ SDK）");
                Console.ResetColor();
            }
        }

        // 2. Obfuscar（可选，仅当用户开启混淆时检测）
        if (opts.EnableObfuscation)
        {
            result.ObfuscarPath = Obfuscator.FindObfuscar();
            result.ObfuscarAvailable = !string.IsNullOrEmpty(result.ObfuscarPath);
            if (!result.ObfuscarAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [!] 未检测到 Obfuscar（混淆脚本仍会生成，但执行时会失败）");
                Console.WriteLine("      请安装: dotnet tool install -g Obfuscar.GlobalTool");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [✓] Obfuscar: {result.ObfuscarPath}");
                Console.ResetColor();
            }
        }

        // 3. Inno Setup（可选，仅当用户开启打包时检测）
        if (opts.EnablePackaging)
        {
            result.InnoSetupPath = InstallerPackager.FindInnoSetup();
            result.InnoSetupAvailable = !string.IsNullOrEmpty(result.InnoSetupPath);
            if (!result.InnoSetupAvailable)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  [!] 未检测到 Inno Setup（打包脚本仍会生成，但执行时会失败）");
                Console.WriteLine("      请安装: https://jrsoftware.org/isdl.php");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [✓] Inno Setup: {result.InnoSetupPath}");
                Console.ResetColor();
            }
        }

        return result;
    }

    /// <summary>检测 dotnet 命令可用性。</summary>
    private static bool CheckDotnet(out string? version)
    {
        version = null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);
            if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                version = output;
                return true;
            }
        }
        catch { }
        return false;
    }

    /// <summary>检测已安装的 .NET SDK 中是否有主版本 >= 指定版本的（dotnet --list-sdks）。</summary>
    private static bool CheckSdkMajorAtLeast(int major)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--list-sdks",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // 形如 "8.0.404 [C:\Program Files\dotnet\sdk]"
                var verPart = line.Trim().Split(' ')[0];
                if (int.TryParse(verPart.Split('.')[0], out var maj) && maj >= major)
                    return true;
            }
        }
        catch { }
        return false;
    }
}
