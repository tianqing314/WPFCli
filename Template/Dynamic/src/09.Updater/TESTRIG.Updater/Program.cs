using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace TESTRIG.Updater;

/// <summary>
/// TESTRIG 升级器：独立于主程序运行（升级时被拷到 %TEMP% 执行），负责
/// 等主进程退出 → 解压新版 → 回填保留项 → 整目录换名（近似原子）→ 启动新版并自检 → 失败自动回滚。
/// 也承担手动回滚（--rollback，与上一版本目录互换）。
/// </summary>
/// <remarks>
/// 用法：
///   升级：TESTRIG.Updater.exe --pid 1234 --app "D:\TESTRIG\app" --zip "D:\TESTRIG\app\updates\x.zip" --exe TESTRIG.App.exe
///   回滚：TESTRIG.Updater.exe --pid 1234 --app "D:\TESTRIG\app" --rollback --exe TESTRIG.App.exe
/// 目录约定（与 app 同级）：
///   &lt;app&gt;.backup  上一版本完整副本（唯一保留的历史版本）
///   &lt;app&gt;.new     解压临时目录（成功后即消失）
///   &lt;app&gt;.failed  自检失败被换下的坏版本（仅诊断用，下次升级清理）
///   &lt;app&gt;.update.log  升级/回滚日志（追加）
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 升级时不覆盖、回滚时随身带走的保留项（目录）。
    /// </summary>
    private static readonly string[] PreservedDirs = { "Config", "Data", "logs" };

    /// <summary>
    /// 保留项（单文件）。
    /// </summary>
    private static readonly string[] PreservedFiles = { "appsettings.json" };

    /// <summary>
    /// 保留项（通配）：Inno 安装器留在程序目录里的卸载器 unins000.exe/.dat/.msg。
    /// 升级包由 publish 产出、不含卸载器，不带走则升级一次后控制面板里卸载不掉（序号未必是 000，故用通配）。
    /// </summary>
    private static readonly string[] PreservedGlobs = { "unins*.*" };

    /// <summary>
    /// Inno 卸载记录注册表子键（AppId 与 installer\TESTRIG.iss 的 AppId 严格一致，改一处必须改另一处）。
    /// </summary>
    private const string UninstallKey =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{6F3B9C2E-4A71-4E2C-9A3D-9B1C0A7D5E01}_is1";

    /// <summary>
    /// 日志文件路径（&lt;app&gt;.update.log）。
    /// </summary>
    private static string _logPath = "";

    /// <summary>
    /// 操作员（--operator 传入，写进 logs/update-*.log）。
    /// </summary>
    private static string _operator = "未知";

    /// <summary>
    /// 入口：解析参数并按模式执行，任何未捕获异常都落日志并弹窗。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>0 成功；非 0 失败。</returns>
    [STAThread]
    private static int Main(string[] args)
    {
        var opts = ParseArgs(args);
        if (opts == null || string.IsNullOrEmpty(opts.AppDir) || string.IsNullOrEmpty(opts.ExeName))
        {
            MessageBox.Show(
                "参数不完整。\n升级：--pid <pid> --app <目录> --zip <包> --exe <主程序>\n回滚：--pid <pid> --app <目录> --rollback --exe <主程序>",
                "TESTRIG 升级器", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }

        var appDir = Path.GetFullPath(opts.AppDir).TrimEnd('\\', '/');
        _logPath = appDir + ".update.log";
        _operator = string.IsNullOrWhiteSpace(opts.Operator) ? "未知" : opts.Operator.Trim();

        // 关键：把自身工作目录挪出程序目录。升级器可能继承主程序的 CWD（= 程序目录），
        // 占着目录会让 Directory.Move 换名必败（共享冲突）。
        Directory.SetCurrentDirectory(Path.GetTempPath());

        try
        {
            Log($"===== {(opts.Rollback ? "回滚" : "升级")}开始 ===== app={appDir} zip={opts.ZipPath}");

            if (!WaitProcessExit(opts.Pid, TimeSpan.FromSeconds(30)))
            {
                throw new InvalidOperationException($"等待主进程(PID={opts.Pid})退出超时，已取消操作。");
            }

            if (opts.Rollback)
            {
                DoRollback(appDir, opts.ExeName);
            }
            else
            {
                DoUpdate(appDir, opts.ZipPath, opts.ExeName);
            }

            Log("===== 完成 =====");
            return 0;
        }
        catch (Exception ex)
        {
            Log("失败：" + ex);
            WriteUserLog(appDir, opts.Rollback ? "回滚" : "升级", "?", "?", "失败：" + ex.Message);
            MessageBox.Show(
                $"{(opts.Rollback ? "回滚" : "升级")}失败：{ex.Message}\n\n详见日志：{_logPath}\n原程序未被破坏，可直接重新启动。",
                "TESTRIG 升级器", MessageBoxButtons.OK, MessageBoxIcon.Error);

            // 尽力把程序拉回可用状态
            TryStartApp(appDir, opts.ExeName);
            return 1;
        }
    }

    /// <summary>
    /// 升级主流程：解压 → 回填保留项 → 双改名换目录 → 启动自检（失败自动回滚）。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="zipPath">新版本 zip 包。</param>
    /// <param name="exeName">主程序文件名。</param>
    private static void DoUpdate(string appDir, string zipPath, string exeName)
    {
        if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
        {
            throw new FileNotFoundException("升级包不存在", zipPath);
        }

        var newDir = appDir + ".new";
        var backupDir = appDir + ".backup";
        var failedDir = appDir + ".failed";

        // 换目录前先记下现版本号，换完就读不到了
        var fromVer = ExeVersion(appDir, exeName);

        // 1) 清场并解压（换目录前完成全部重活，压缩包坏在这一步就会暴露，主程序目录不受影响）
        DeleteDir(newDir);
        DeleteDir(failedDir);
        Log("解压升级包 …");
        ZipFile.ExtractToDirectory(zipPath, newDir);
        if (!File.Exists(Path.Combine(newDir, exeName)))
        {
            throw new InvalidOperationException($"升级包内未找到主程序 {exeName}，包可能损坏。");
        }

        // 2) 保留项回填：新目录使用现网 Config/Data/logs/appsettings.json + 卸载器（永不覆盖原则）
        Log("回填保留项（Config/Data/logs/appsettings.json/unins*）…");
        CopyPreserved(appDir, newDir);

        // 3) 双改名换目录：app → backup（先腾掉旧 backup），new → app；任一步失败反向还原
        Log("切换目录 …");
        DeleteDir(backupDir);
        MoveWithRetry(appDir, backupDir);
        try
        {
            MoveWithRetry(newDir, appDir);
        }
        catch
        {
            MoveWithRetry(backupDir, appDir); // 还原
            throw;
        }

        // 此刻 appDir 已是新版本目录
        var toVer = ExeVersion(appDir, exeName);

        // 4) 启动新版并自检：60s 内退出视为坏版本 → 自动回滚（坏版本留 .failed 供诊断）
        Log("启动新版本自检 …");
        var proc = StartApp(appDir, exeName);
        if (!SurvivedSelfCheck(proc, TimeSpan.FromSeconds(60)))
        {
            Log("新版本自检失败（60s 内退出），自动回滚 …");
            CopyPreserved(appDir, backupDir); // 新版可能已写数据，随身带回
            MoveWithRetry(appDir, failedDir);
            MoveWithRetry(backupDir, appDir);
            TryStartApp(appDir, exeName);
            WriteUserLog(appDir, "升级", fromVer, toVer, "失败：新版本启动自检未通过，已自动回滚");
            throw new InvalidOperationException("新版本启动自检失败，已自动回滚到上一版本（坏版本保留在 *.failed 供诊断）。");
        }

        // 5) 成功：清理升级包（它在 backup 的 updates/ 里，顺带瘦身）
        TryDelete(zipPath);
        SyncUninstallRecord(appDir, exeName);
        WriteUserLog(appDir, "升级", fromVer, toVer, "成功");
        Log("升级成功。上一版本保留在 " + backupDir);
    }

    /// <summary>
    /// 手动回滚：当前版本与 .backup 互换（可再滚回来），保留项随身迁移保证数据/配置连续。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="exeName">主程序文件名。</param>
    private static void DoRollback(string appDir, string exeName)
    {
        var backupDir = appDir + ".backup";
        var tmpDir = appDir + ".swap";
        if (!Directory.Exists(backupDir) || !File.Exists(Path.Combine(backupDir, exeName)))
        {
            throw new InvalidOperationException("没有可回滚的上一版本（*.backup 不存在或不完整）。");
        }

        // 互换前记下两边版本号
        var fromVer = ExeVersion(appDir, exeName);
        var toVer = ExeVersion(backupDir, exeName);

        // 保留项从当前版本带到被提升的旧版本（测试数据/配置不回退）
        Log("迁移保留项到上一版本 …");
        CopyPreserved(appDir, backupDir);

        Log("互换目录 …");
        DeleteDir(tmpDir);
        MoveWithRetry(appDir, tmpDir);
        try
        {
            MoveWithRetry(backupDir, appDir);
        }
        catch
        {
            MoveWithRetry(tmpDir, appDir); // 还原
            throw;
        }
        MoveWithRetry(tmpDir, backupDir);

        SyncUninstallRecord(appDir, exeName);
        WriteUserLog(appDir, "回滚", fromVer, toVer, "成功");
        StartApp(appDir, exeName);
        Log("回滚完成（原版本已成为新的 *.backup，可再次前滚）。");
    }

    /// <summary>
    /// 把控制面板「程序和功能」里的卸载记录对齐到换装后的实际版本。
    /// 在线升级不走 Inno 安装器，注册表没人改，不同步则现场看到的版本号长期停在装机那一版。
    /// HKLM 写不动（操作员非管理员）只记日志，绝不因此判升级失败——版本号显示不对不值得回滚一次升级。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="exeName">主程序文件名（版本号取自它的文件版本）。</param>
    private static void SyncUninstallRecord(string appDir, string exeName)
    {
        try
        {
            var exePath = Path.Combine(appDir, exeName);
            if (!File.Exists(exePath))
            {
                return;
            }

            var fv = FileVersionInfo.GetVersionInfo(exePath);
            var version = $"{fv.FileMajorPart}.{fv.FileMinorPart}.{fv.FileBuildPart}";

            // 安装器可能以 32/64 位视图、按机器或按用户写入，四种组合挨个试
            var hives = new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            var views = new[] { RegistryView.Registry64, RegistryView.Registry32 };
            var written = false;
            foreach (var hive in hives)
            {
                foreach (var view in views)
                {
                    try
                    {
                        using (var root = RegistryKey.OpenBaseKey(hive, view))
                        using (var key = root.OpenSubKey(UninstallKey, true))
                        {
                            if (key == null)
                            {
                                continue;
                            }

                            key.SetValue("DisplayVersion", version);
                            key.SetValue("InstallLocation", appDir + "\\");
                            written = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"同步卸载记录（{hive}/{view}）失败：{ex.Message}");
                    }
                }
            }

            Log(written ? $"卸载记录已更新为 v{version}。" : "未找到卸载记录（可能是解压部署而非安装包安装），跳过同步。");
        }
        catch (Exception ex)
        {
            Log("同步卸载记录失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 把保留项（目录整棵 + 单文件）从 <paramref name="fromDir"/> 复制覆盖到 <paramref name="toDir"/>。
    /// </summary>
    /// <param name="fromDir">来源程序目录。</param>
    /// <param name="toDir">目标程序目录。</param>
    private static void CopyPreserved(string fromDir, string toDir)
    {
        foreach (var d in PreservedDirs)
        {
            var src = Path.Combine(fromDir, d);
            if (Directory.Exists(src))
            {
                CopyDir(src, Path.Combine(toDir, d));
            }
        }

        foreach (var f in PreservedFiles)
        {
            var src = Path.Combine(fromDir, f);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(toDir, f), true);
            }
        }

        foreach (var pattern in PreservedGlobs)
        {
            foreach (var src in Directory.GetFiles(fromDir, pattern))
            {
                File.Copy(src, Path.Combine(toDir, Path.GetFileName(src)), true);
            }
        }
    }

    /// <summary>
    /// 递归复制目录（覆盖同名文件）。
    /// </summary>
    /// <param name="src">源目录。</param>
    /// <param name="dst">目标目录。</param>
    private static void CopyDir(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src))
        {
            File.Copy(file, Path.Combine(dst, Path.GetFileName(file)), true);
        }

        foreach (var dir in Directory.GetDirectories(src))
        {
            CopyDir(dir, Path.Combine(dst, Path.GetFileName(dir)));
        }
    }

    /// <summary>
    /// 目录改名，带重试（杀毒/索引服务可能短暂占用）。
    /// </summary>
    /// <param name="from">原路径。</param>
    /// <param name="to">新路径。</param>
    private static void MoveWithRetry(string from, string to)
    {
        const int MaxAttempts = 10;
        for (var i = 1; ; i++)
        {
            try
            {
                Directory.Move(from, to);
                return;
            }
            catch (IOException) when (i < MaxAttempts)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException) when (i < MaxAttempts)
            {
                Thread.Sleep(500);
            }
        }
    }

    /// <summary>
    /// 等待指定进程退出。
    /// </summary>
    /// <param name="pid">进程 ID（0 表示无需等待）。</param>
    /// <param name="timeout">超时。</param>
    /// <returns>是否已退出。</returns>
    private static bool WaitProcessExit(int pid, TimeSpan timeout)
    {
        if (pid <= 0)
        {
            return true;
        }

        try
        {
            using var p = Process.GetProcessById(pid);
            return p.WaitForExit((int)timeout.TotalMilliseconds);
        }
        catch (ArgumentException)
        {
            return true; // 进程已不存在
        }
    }

    /// <summary>
    /// 启动主程序。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="exeName">主程序文件名。</param>
    /// <returns>进程。</returns>
    private static Process StartApp(string appDir, string exeName)
    {
        return Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(appDir, exeName),
            WorkingDirectory = appDir,
            UseShellExecute = true,
        });
    }

    /// <summary>
    /// 尽力启动主程序（失败仅记日志，不再抛出）。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="exeName">主程序文件名。</param>
    private static void TryStartApp(string appDir, string exeName)
    {
        try
        {
            if (File.Exists(Path.Combine(appDir, exeName)))
            {
                StartApp(appDir, exeName);
            }
        }
        catch (Exception ex)
        {
            Log("重启主程序失败：" + ex.Message);
        }
    }

    /// <summary>
    /// 自检：进程在观察窗口内保持存活，或以退出码 0 正常退出（操作员升级后立即关闭程序），
    /// 均视为启动成功；只有窗口内**非零退出码**（崩溃）才判失败触发自动回滚。
    /// </summary>
    /// <param name="proc">被观察进程。</param>
    /// <param name="window">观察时长。</param>
    /// <returns>是否视为成功。</returns>
    private static bool SurvivedSelfCheck(Process proc, TimeSpan window)
    {
        if (proc == null)
        {
            return false;
        }

        if (!proc.WaitForExit((int)window.TotalMilliseconds))
        {
            return true; // 存活到观察窗口结束
        }

        Log($"新版本在自检窗口内退出，退出码={proc.ExitCode}");
        return proc.ExitCode == 0;
    }

    /// <summary>
    /// 删除目录（存在才删，带重试）。
    /// </summary>
    /// <param name="dir">目录。</param>
    private static void DeleteDir(string dir)
    {
        if (!Directory.Exists(dir))
        {
            return;
        }

        for (var i = 1; ; i++)
        {
            try
            {
                Directory.Delete(dir, true);
                return;
            }
            catch (IOException) when (i < 5)
            {
                Thread.Sleep(400);
            }
            catch (UnauthorizedAccessException) when (i < 5)
            {
                Thread.Sleep(400);
            }
        }
    }

    /// <summary>
    /// 尽力删除文件（失败忽略）。
    /// </summary>
    /// <param name="path">文件路径。</param>
    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // 清理失败不影响结果
        }
    }

    /// <summary>
    /// 读取指定目录内主程序的文件版本（前三段）；取不到返回 "?"。
    /// </summary>
    /// <param name="dir">程序目录。</param>
    /// <param name="exeName">主程序文件名。</param>
    /// <returns>x.y.z 或 "?"。</returns>
    private static string ExeVersion(string dir, string exeName)
    {
        try
        {
            var path = Path.Combine(dir, exeName);
            if (!File.Exists(path))
            {
                return "?";
            }

            var fv = FileVersionInfo.GetVersionInfo(path);
            return $"{fv.FileMajorPart}.{fv.FileMinorPart}.{fv.FileBuildPart}";
        }
        catch
        {
            return "?";
        }
    }

    /// <summary>
    /// 写运维日志 &lt;app&gt;\logs\update-yyyyMM.log：从哪版到哪版、谁操作的、结果如何。
    /// 与 &lt;app&gt;.update.log 分工不同——那份是换装过程的技术流水，这份是给产线追溯看的。
    /// logs 在保留项里，换目录后仍是同一份，历史不断档。
    /// </summary>
    /// <param name="appDir">程序目录。</param>
    /// <param name="action">升级 / 回滚。</param>
    /// <param name="from">原版本。</param>
    /// <param name="to">目标版本。</param>
    /// <param name="result">结果描述。</param>
    private static void WriteUserLog(string appDir, string action, string from, string to, string result)
    {
        try
        {
            var dir = Path.Combine(appDir, "logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"update-{DateTime.Now:yyyyMM}.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {action} v{from} → v{to} | 操作员 {_operator} | {result}";
            File.AppendAllText(file, line + "\r\n", Encoding.UTF8);
        }
        catch
        {
            // 运维日志写不进去不能阻断升级本身
        }
    }

    /// <summary>
    /// 追加一行日志到 &lt;app&gt;.update.log（升级器自身无 UI，日志是唯一过程凭据）。
    /// </summary>
    /// <param name="message">内容。</param>
    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(_logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n", Encoding.UTF8);
        }
        catch
        {
            // 日志写不进去也不能阻断升级
        }
    }

    /// <summary>
    /// 命令行参数。
    /// </summary>
    private sealed class Options
    {
        /// <summary>主进程 PID。</summary>
        public int Pid;

        /// <summary>程序目录。</summary>
        public string AppDir = "";

        /// <summary>升级包路径（升级模式）。</summary>
        public string ZipPath = "";

        /// <summary>主程序文件名。</summary>
        public string ExeName = "";

        /// <summary>是否回滚模式。</summary>
        public bool Rollback;

        /// <summary>操作员（写进 logs/update-*.log，供追溯谁在哪台机器升的级）。</summary>
        public string Operator = "";
    }

    /// <summary>
    /// 解析 --key value / --rollback 形式的参数。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>解析结果；无参返回 null。</returns>
    private static Options? ParseArgs(IReadOnlyList<string> args)
    {
        if (args == null || args.Count == 0)
        {
            return null;
        }

        var o = new Options();
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--pid" when i + 1 < args.Count:
                    int.TryParse(args[++i], out o.Pid);
                    break;
                case "--app" when i + 1 < args.Count:
                    o.AppDir = args[++i];
                    break;
                case "--zip" when i + 1 < args.Count:
                    o.ZipPath = args[++i];
                    break;
                case "--exe" when i + 1 < args.Count:
                    o.ExeName = args[++i];
                    break;
                case "--operator" when i + 1 < args.Count:
                    o.Operator = args[++i];
                    break;
                case "--rollback":
                    o.Rollback = true;
                    break;
            }
        }

        return o;
    }
}
