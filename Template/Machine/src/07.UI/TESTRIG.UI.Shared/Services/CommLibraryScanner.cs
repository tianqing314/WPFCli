using System.IO;
using System.Reflection;

namespace TESTRIG.UI.Shared.Services;

/// <summary>
/// 通讯库实例条目：refdlls 的 Xmas11.Comm.Devices.*.dll 中一个通讯驱动类（如 DPSEXBase）。
/// </summary>
/// <param name="TypeName">通讯类名（下拉显示，如 DPSEXBase）。</param>
/// <param name="Model">标准模块型号（TypeName 去 Base 后缀，如 DPSEX）——驱动注册键，按 [DutDriver] 匹配。</param>
public sealed record CommLibraryEntry(string TypeName, string Model);

/// <summary>
/// 通讯库扫描器：枚举产物目录（bin）中 <c>Xmas11.Comm.Devices.*.dll</c>（来源 Template\Common\refdlls）
/// 的通讯驱动类，供测试项维护页「共享设备 → 通讯库实例」下拉选择。新通讯库 dll 拷入 refdlls 并
/// 由 csproj 引用后，重新生成产物即可在下拉中出现，无需改代码。
/// </summary>
public static class CommLibraryScanner
{
    /// <summary>
    /// 扫描当前产物目录的 Xmas11 设备通讯库，返回通讯类清单（按程序集/类型名排序）。
    /// </summary>
    /// <returns>通讯类清单。</returns>
    public static IReadOnlyList<CommLibraryEntry> Scan()
    {
        var result = new List<CommLibraryEntry>();
        var dir = AppContext.BaseDirectory;
        if (!Directory.Exists(dir))
        {
            return result;
        }

        foreach (var dll in Directory.EnumerateFiles(dir, "Xmas11.Comm.Devices.*.dll").OrderBy(f => f))
        {
            try
            {
                var asm = Assembly.LoadFrom(dll);
                foreach (var t in asm.GetTypes())
                {
                    if (!t.IsPublic || t.IsAbstract || t.FullName?.StartsWith("Xmas11.Comm.Devices") != true)
                    {
                        continue;
                    }

                    // 通讯驱动基类约定以 Base 结尾（DPSEXBase / DPC2Base 等）
                    if (!t.Name.EndsWith("Base", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    result.Add(new CommLibraryEntry(t.Name, t.Name[..^"Base".Length]));
                }
            }
            catch
            {
                // 单个通讯库加载失败（缺依赖等）不影响其余
            }
        }

        return result.OrderBy(e => e.TypeName, StringComparer.Ordinal).ToList();
    }
}
