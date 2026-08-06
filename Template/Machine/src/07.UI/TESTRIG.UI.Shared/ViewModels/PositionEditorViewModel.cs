using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Core.Abstractions;
using TESTRIG.UI.Shared.Services;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 号位编辑器：编辑号位序号/名称 + 完整连接端点（无 / 网络 / 串口 / USB）。
/// 编辑工作副本，确认（<see cref="Confirmed"/>=true）后由主页面回写。
/// </summary>
public partial class PositionEditorViewModel : ObservableObject
{
    /// <summary>
    /// 编辑中的号位（工作副本，Index/Name 直接绑定）。
    /// </summary>
    public PositionEditModel Position { get; }

    /// <summary>
    /// 通讯方式可选项。
    /// </summary>
    public IReadOnlyList<string> CommKinds { get; } = ["无", "网络", "串口", "USB"];

    /// <summary>
    /// 停止位可选项。
    /// </summary>
    public IReadOnlyList<string> StopBitsOptions { get; } = ["One", "Two", "OnePointFive", "None"];

    /// <summary>
    /// 校验方式可选项。
    /// </summary>
    public IReadOnlyList<string> ParityOptions { get; } = ["None", "Odd", "Even", "Mark", "Space"];

    /// <summary>
    /// 是否点了确定。
    /// </summary>
    public bool Confirmed { get; private set; }

    /// <summary>
    /// 请求关闭窗口（参数=是否确认）。
    /// </summary>
    public event Action<bool>? CloseRequested;

    /// <summary>
    /// 用工作副本构造，并从其现有端点回填字段。
    /// </summary>
    /// <param name="working">工作副本。</param>
    public PositionEditorViewModel(PositionEditModel working)
    {
        Position = working;
        LoadFrom(working.Comm);
    }

    // ===== 端点字段 =====

    /// <summary>通讯方式（无/网络/串口/USB），驱动字段可见性。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEthernet))]
    [NotifyPropertyChangedFor(nameof(IsSerial))]
    [NotifyPropertyChangedFor(nameof(IsUsb))]
    private string _commKind = "无";

    /// <summary>网络 IP。</summary>
    [ObservableProperty] private string _ip = "";

    /// <summary>网络端口。</summary>
    [ObservableProperty] private string _port = "";

    /// <summary>串口/USB 物理链路号。</summary>
    [ObservableProperty] private string _physicalLink = "";

    /// <summary>串口波特率。</summary>
    [ObservableProperty] private string _baud = "9600";

    /// <summary>串口数据位。</summary>
    [ObservableProperty] private string _dataBits = "8";

    /// <summary>串口停止位。</summary>
    [ObservableProperty] private string _stopBits = "One";

    /// <summary>串口校验。</summary>
    [ObservableProperty] private string _parity = "None";

    /// <summary>USB 厂商 ID（十六进制显示/输入，如 2E19）。</summary>
    [ObservableProperty] private string _vid = "";

    /// <summary>USB 产品 ID（十六进制显示/输入，如 0001）。</summary>
    [ObservableProperty] private string _pid = "";

    /// <summary>是否网络端点（字段可见性）。</summary>
    public bool IsEthernet => CommKind == "网络";

    /// <summary>是否串口端点。</summary>
    public bool IsSerial => CommKind == "串口";

    /// <summary>是否 USB 端点。</summary>
    public bool IsUsb => CommKind == "USB";

    /// <summary>
    /// 从现有端点回填字段。
    /// </summary>
    /// <param name="c">端点（null=无）。</param>
    private void LoadFrom(CommEndpoint? c)
    {
        if (c is null)
        {
            CommKind = "无";
            return;
        }

        switch (c.Link)
        {
            case LinkType.Ethernet:
                CommKind = "网络";
                Ip = c.Ip ?? "";
                Port = c.Port?.ToString(CultureInfo.InvariantCulture) ?? "";
                break;
            case LinkType.Serial:
                CommKind = "串口";
                PhysicalLink = c.PhysicalLink ?? "";
                Baud = (c.Serial?.Baud ?? 9600).ToString(CultureInfo.InvariantCulture);
                DataBits = (c.Serial?.DataBits ?? 8).ToString(CultureInfo.InvariantCulture);
                StopBits = c.Serial?.StopBits ?? "One";
                Parity = c.Serial?.Parity ?? "None";
                break;
            case LinkType.Usb:
                CommKind = "USB";
                PhysicalLink = c.PhysicalLink ?? "";
                Vid = c.Vid is { } v ? $"0x{v:X4}" : "";
                Pid = c.Pid is { } p ? $"0x{p:X4}" : "";
                break;
        }
    }

    /// <summary>
    /// 按当前字段构建端点；返回 (是否成功, 端点或 null, 错误信息)。
    /// </summary>
    private (bool Ok, CommEndpoint? Comm, string? Error) BuildComm()
    {
        switch (CommKind)
        {
            case "无":
                return (true, null, null);
            case "网络":
                if (string.IsNullOrWhiteSpace(Ip)) { return (false, null, "网络端点需填写 IP。"); }
                if (!int.TryParse(Port, out var port)) { return (false, null, "端口必须为整数。"); }
                return (true, CommEndpoint.OfEthernet(Ip.Trim(), port), null);
            case "串口":
                if (string.IsNullOrWhiteSpace(PhysicalLink)) { return (false, null, "串口端点需填写物理链路号。"); }
                if (!int.TryParse(Baud, out var baud) || !int.TryParse(DataBits, out var db))
                {
                    return (false, null, "波特率/数据位必须为整数。");
                }
                return (true, CommEndpoint.OfSerial(PhysicalLink.Trim(), new SerialParams(baud, db, StopBits, Parity)), null);
            case "USB":
                if (string.IsNullOrWhiteSpace(PhysicalLink)) { return (false, null, "USB 端点需填写物理链路号。"); }
                if (!TryHex(Vid, out var vid) || !TryHex(Pid, out var pid))
                {
                    return (false, null, "VID/PID 必须为十六进制（如 2E19，可带 0x 前缀）。");
                }
                return (true, CommEndpoint.OfUsb(PhysicalLink.Trim(), vid, pid), null);
            default:
                return (true, null, null);
        }
    }

    /// <summary>
    /// 解析十六进制（可带 0x 前缀）为整数。
    /// </summary>
    /// <param name="s">十六进制串。</param>
    /// <param name="value">解析结果。</param>
    /// <returns>是否解析成功。</returns>
    private static bool TryHex(string s, out int value)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }

        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>确定：校验并写回端点后关闭。</summary>
    [RelayCommand]
    private void Confirm()
    {
        if (string.IsNullOrWhiteSpace(Position.Name))
        {
            AppDialog.Error("无法保存", "号位名称不能为空。");
            return;
        }

        var (ok, comm, error) = BuildComm();
        if (!ok)
        {
            AppDialog.Error("无法保存", error!);
            return;
        }

        Position.Comm = comm;
        Position.NotifyCommChanged();
        Confirmed = true;
        CloseRequested?.Invoke(true);
    }

    /// <summary>取消。</summary>
    [RelayCommand]
    private void Cancel()
    {
        Confirmed = false;
        CloseRequested?.Invoke(false);
    }
}
