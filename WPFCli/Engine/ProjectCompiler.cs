using System.Diagnostics;
using System.Text;
using WPFCli.Models;

namespace WPFCli.Engine;

/// <summary>
/// 项目编译器 —— 调用 dotnet build 编译生成的解决方案，实时推送输出。
/// </summary>
public static class ProjectCompiler
{
    /// <summary>默认编译超时（10 分钟 —— DeviceLink 多目标库首次全量编译可能超过 5 分钟）。</summary>
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(10);

    /// <summary>编译解决方案。</summary>
    /// <param name="slnPath">.sln 文件绝对路径。</param>
    /// <param name="configuration">编译配置（Debug/Release）。</param>
    /// <param name="onOutput">实时输出回调（stdout + stderr）。</param>
    /// <param name="timeout">超时（默认 10 分钟）。</param>
    /// <returns>退出码 0 视为成功；超时返回 -1。</returns>
    public static async Task<int> CompileAsync(string slnPath, string configuration,
        Action<string>? onOutput = null, TimeSpan? timeout = null)
    {
        if (!File.Exists(slnPath))
            throw new FileNotFoundException($"解决方案文件不存在: {slnPath}");

        using var cts = new CancellationTokenSource(timeout ?? DefaultTimeout);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // 必须 CreateNoWindow=true 并禁用 StandardOutput 阻塞读取
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(slnPath) ?? Environment.CurrentDirectory
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(slnPath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add(configuration);
        psi.ArgumentList.Add("--nologo");
        // WPF 解决方案并行构建时，临时 XAML 项目偶发无诊断退出（0 warning / 0 error / exit 1）。
        // 与仓库验证命令保持一致，串行构建可稳定生成应用和 Updater。
        psi.ArgumentList.Add("-m:1");

        using var proc = new Process { StartInfo = psi };

        // 异步消费 stdout/stderr 防止管道死锁
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                try { onOutput?.Invoke(e.Data); }
                catch { /* 回调异常不影响编译 */ }
            }
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                try { onOutput?.Invoke($"[stderr] {e.Data}"); }
                catch { }
            }
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(cts.Token);
            return proc.ExitCode;
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!proc.HasExited) proc.Kill(entireProcessTree: true);
            }
            catch { }
            onOutput?.Invoke("[TIMEOUT] dotnet build 超时，已强制终止");
            return -1;
        }
    }

    /// <summary>编译 BuildOptions 描述的项目。</summary>
    public static async Task<int> CompileAsync(BuildOptions opts, Action<string>? onOutput = null,
        TimeSpan? timeout = null)
        => await CompileAsync(opts.SolutionPath, opts.Template.Configuration, onOutput, timeout);
}
