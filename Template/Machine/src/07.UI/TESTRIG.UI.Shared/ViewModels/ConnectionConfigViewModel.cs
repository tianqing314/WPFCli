using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TESTRIG.Core.Abstractions;
using TESTRIG.Devices;
using TESTRIG.Devices.Comm;
using TESTRIG.Infrastructure.Configuration;
using TESTRIG.UI.Shared.Services;

namespace TESTRIG.UI.Shared.ViewModels;

/// <summary>
/// 连接配置 v3：分两个 Tab——
/// 「共享设备」(PLC + 标准盒子设备，所有针床通用) 与「被检连接」(按号位 = 拼版数)。
/// 每行可选 网络/串口/USB 并各带「连接/断开」按钮；标准盒另有「一键连接」。
/// 组连接状态（标准盒/PLC）= 组内全部设备连接成功才算连接，并与主页面顶部状态栏保持一致。
/// 被检在测试任务开始时由继电器上电，故配置阶段不连接。
/// </summary>
public partial class ConnectionConfigViewModel : ObservableObject
{
    /// <summary>
    /// ConST 自家设备 USB VID。USB 扫描只列这个 VID 的设备。
    /// </summary>
    public const int ConStVid = 0x2E19;

    /// <summary>
    /// 全部可选通讯方式（网络/串口/USB），用于被检行。
    /// </summary>
    private static readonly LinkType[] AllLinks = [LinkType.Ethernet, LinkType.Serial, LinkType.Usb];

    /// <summary>
    /// 仅网络（PLC 与多数标准盒子固定用网络）。
    /// </summary>
    private static readonly LinkType[] EthernetOnly = [LinkType.Ethernet];

    /// <summary>
    /// 仅串口（ConST326 固定用串口）。
    /// </summary>
    private static readonly LinkType[] SerialOnly = [LinkType.Serial];

    /// <summary>
    /// 连接配置持久化仓储。
    /// </summary>
    private readonly IConnectionConfigStore _store;

    /// <summary>
    /// 设备扫描器（串口/USB）。
    /// </summary>
    private readonly IDeviceScanner _scanner;

    /// <summary>
    /// 连接管理器（真实共享设备的连接/断开）。
    /// </summary>
    private readonly ConnectionManager _conn;

    /// <summary>
    /// 当前针床 Key，用于按针床存取被检连接配置。
    /// </summary>
    private readonly string _boardKey;

    /// <summary>
    /// 被检描述符（型号/键），用于被检行的连通性测试。
    /// </summary>
    private readonly DeviceDescriptor _dut;

    /// <summary>
    /// 标准模块型号表（manifest ToolDevices Key → Model，连通性测试用）。
    /// </summary>
    private readonly Dictionary<string, string> _toolModel = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 标准模块配置序列号表（manifest ToolDevices Key → DevSn，连接时读设备 SN 比对）。
    /// </summary>
    private readonly Dictionary<string, string?> _toolSn = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 构造连接配置视图模型：按当前配置与 manifest 号位构建各连接行，并后台扫描设备。
    /// </summary>
    /// <param name="store">连接配置仓储。</param>
    /// <param name="scanner">设备扫描器。</param>
    /// <param name="conn">连接管理器。</param>
    /// <param name="manifest">当前针床清单。</param>
    /// <param name="sharedStore">共享设备配置仓储（测试项维护写入的 .shared.json）。</param>
    public ConnectionConfigViewModel(IConnectionConfigStore store, IDeviceScanner scanner, ConnectionManager conn,
        JigManifest manifest, ISharedDeviceStore sharedStore)
    {
        _store = store;
        _scanner = scanner;
        _conn = conn;
        _boardKey = manifest.Key;
        _dut = manifest.Dut;
        var c = store.Current;

        // 标准模块（共享设备：正压/真空标准模块，来自测试项维护的共享设备清单，存在即完全取代 manifest 默认；
        // 已存 StandardModules 的 COM 覆盖优先）。通讯方式按清单（串口/网口），各自一行带连接/断开（真连探活）。
        // 物理链路只选 COM 口——序列号不作为物理链路显示，连接时由驱动读设备 SN 与清单 SerialNumber 比对。
        var toolSource = sharedStore.Load(manifest.DeviceFamily, manifest.Key) ?? manifest.ToolDevices;
        foreach (var tool in toolSource)
        {
            _toolModel[tool.Key] = tool.Model;
            _toolSn[tool.Key] = tool.SerialNumber; // 清单序列号（如 DPSE022FE0054，可空）
            var ep = c.StandardModules.TryGetValue(tool.Key, out var se)
                ? se
                : tool.Comm is not null
                    ? tool.Comm with { PhysicalLink = "" }
                    : CommEndpoint.OfSerial("", new SerialParams(4800, 8, "Two", "None"));
            StandardModules.Add(new CommRowViewModel(tool.Name, ep, SerialOptions, UsbOptions, conn, AllLinks)
            {
                ToggleHandler = ToggleStandardModuleRowAsync,
                DeviceKey = tool.Key,
            });
        }

        // 被检：按号位展开，默认取 manifest 号位 Comm，已存配置优先；每号位带连接/断开（真连探活）
        var saved = c.Duts.TryGetValue(_boardKey, out var l) ? l : null;
        for (var i = 0; i < manifest.Positions.Count; i++)
        {
            var pos = manifest.Positions[i];
            var ep = saved is not null && i < saved.Count
                ? saved[i]
                : pos.Comm ?? CommEndpoint.OfEthernet("192.168.40.107", 1030);
            Duts.Add(new CommRowViewModel($"{pos.Name} 被检", ep, SerialOptions, UsbOptions, conn, AllLinks)
            {
                ToggleHandler = ToggleDutRowAsync,
            });
        }

        RecomputeGroupStatus();

        // 后台扫描，不阻塞窗口打开
        _ = RefreshDevices();
    }

    /// <summary>
    /// 串口下拉「不选」占位项：列在物理链路/端口下拉首位，供选错后清空重选（PortChain 空 = 未指定）。
    /// </summary>
    public static readonly DiscoveredSerial NoneSerial = new("（不选）", "", "", "（不选）");

    /// <summary>
    /// 串口下拉可选项（首项为「不选」占位，其余为扫描结果）。
    /// </summary>
    public ObservableCollection<DiscoveredSerial> SerialOptions { get; } = [NoneSerial];

    /// <summary>
    /// USB 下拉可选项（扫描结果，仅 ConST VID）。
    /// </summary>
    public ObservableCollection<DiscoveredUsb> UsbOptions { get; } = [];

    /// <summary>
    /// 标准模块（共享设备：正压/真空）连接行集合。
    /// </summary>
    public ObservableCollection<CommRowViewModel> StandardModules { get; } = [];

    /// <summary>
    /// 是否配置了共享设备（未配置则连接配置页只显示被检连接，隐藏共享设备 Tab）。
    /// </summary>
    public bool HasStandardModules => StandardModules.Count > 0;

    /// <summary>
    /// 被检连接行集合（按号位）；针床工装模式下承载针床工装子设备行。
    /// </summary>
    public ObservableCollection<CommRowViewModel> Duts { get; } = [];

    /// <summary>
    /// 「被检连接」页标题（整机无针床工装，恒为按号位被检）。
    /// </summary>
    public string DutTabHeader => "被检连接（按号位）";

    /// <summary>
    /// 「被检连接」页副标题。
    /// </summary>
    public string DutTabSubtitle => "被检（按号位 = 拼版数）";

    /// <summary>
    /// 状态栏文字（扫描/连接进度）。
    /// </summary>
    [ObservableProperty] private string _status = "";

    /// <summary>
    /// 标准模块组连接状态（组内全部连接才为 true）。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectStandardLabel))]
    private bool _standardConnected;

    /// <summary>
    /// 标准模块一键连接/断开 按钮文字（随连接状态切换）。
    /// </summary>
    public string ConnectStandardLabel => StandardConnected ? "一键断开" : "一键连接";

    /// <summary>
    /// 请求关闭窗口事件（保存/取消后触发）。
    /// </summary>
    public event EventHandler? RequestClose;

    /// <summary>
    /// 是否有耗时操作进行中（扫描设备 / 一键连接标准盒），驱动覆盖式加载缓冲。
    /// </summary>
    [ObservableProperty] private bool _isBusy;

    /// <summary>
    /// 加载缓冲副标题（当前耗时操作的分步说明）。
    /// </summary>
    [ObservableProperty] private string _busyHint = "";

    /// <summary>
    /// 后台扫描串口与 USB 设备并刷新下拉选项。
    /// </summary>
    [RelayCommand]
    private async Task RefreshDevices()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        BusyHint = "枚举串口 / USB（WMI 扫描）";
        Status = "正在扫描设备…";
        try
        {
            // WMI 扫描（串口/USB）较慢，放后台线程，避免阻塞窗口打开与交互。
            // 不再排除 COM1：ConST326 等设备现场可能就挂在 COM1，滤掉会导致已存物理链路匹配不上、下拉反显为空。
            var serials = await Task.Run(() => _scanner.ScanSerial().ToList());

            // 仅 ConST VID 0x2E19
            var usbs = await Task.Run(() => _scanner.ScanUsb(ConStVid).ToList());
            SerialOptions.Clear();
            SerialOptions.Add(NoneSerial);   // 首位保留「不选」，便于选错后清空
            foreach (var s in serials)
            {
                SerialOptions.Add(s);
            }

            UsbOptions.Clear();
            foreach (var u in usbs)
            {
                UsbOptions.Add(u);
            }

            // 下拉填充前 WPF 会把匹配不到的 SelectedValue 写回 null，此处按已存配置回填各行物理链路
            foreach (var r in AllRows())
            {
                r.ApplySavedSerialSelection();
            }

            Status = $"扫描到 {SerialOptions.Count} 个串口、{UsbOptions.Count} 个 ConST USB 设备";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 枚举全部连接行（标准模块 + 各号位被检）。
    /// </summary>
    /// <returns>连接行序列。</returns>
    private IEnumerable<CommRowViewModel> AllRows()
    {
        foreach (var r in StandardModules)
        {
            yield return r;
        }
        foreach (var r in Duts)
        {
            yield return r;
        }
    }

    /// <summary>
    /// 标准模块「一键连接/断开」：已连接则断开所有，否则真连探活所有。
    /// </summary>
    [RelayCommand]
    private async Task ConnectStandard()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (StandardConnected)
            {
                BusyHint = "断开标准模块";
                foreach (var r in StandardModules) { r.IsConnected = false; r.TestResult = "已断开"; }
                RecomputeGroupStatus();
                Status = "标准模块已全部断开";
            }
            else
            {
                BusyHint = "连接标准模块（真连探活）";
                Status = "正在连接标准模块…";
                foreach (var r in StandardModules)
                {
                    var (ok, msg) = await ConnectStandardModuleAsync(r);
                    r.IsConnected = ok;
                    r.TestResult = (ok ? "✓ " : "✗ ") + msg;
                }
                RecomputeGroupStatus();
                Status = StandardConnected ? "标准模块已全部连接" : "标准模块部分连接失败";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// 标准模块行「连接/断开」：真连探活（按型号建驱动 ConnectAsync），再同步组状态。
    /// </summary>
    /// <param name="row">标准模块连接行。</param>
    private async Task ToggleStandardModuleRowAsync(CommRowViewModel row)
    {
        if (row.IsConnected)
        {
            row.IsConnected = false;
            row.TestResult = "已断开";
        }
        else
        {
            row.TestResult = "正在连接…";
            var (ok, msg) = await ConnectStandardModuleAsync(row);
            row.IsConnected = ok;
            row.TestResult = (ok ? "✓ " : "✗ ") + msg;
        }
        RecomputeGroupStatus();
    }

    /// <summary>
    /// 真连探活一个标准模块（按 DeviceKey 取型号，建驱动 ConnectAsync，测完关闭）。
    /// </summary>
    /// <param name="row">标准模块连接行。</param>
    /// <returns>(是否连通, 说明)。</returns>
    private Task<(bool Ok, string Message)> ConnectStandardModuleAsync(CommRowViewModel row)
    {
        var key = row.DeviceKey ?? "";
        var model = _toolModel.TryGetValue(key, out var m) ? m : key;
        var sn = _toolSn.TryGetValue(key, out var s) ? s : null;
        return _conn.TestStandardModuleAsync(key, row.Name, model, row.ToEndpoint(), sn);
    }

    /// <summary>
    /// 单设备「连接/断开」（被检外其它设备）：物理链路/网络解析；再同步组状态。
    /// </summary>
    /// <param name="row">目标连接行。</param>
    private async Task ToggleRowAsync(CommRowViewModel row)
    {
        if (row.IsConnected)
        {
            row.IsConnected = false;
            row.TestResult = "已断开";
        }
        else
        {
            // 其它（被检外）：物理链路/网络解析
            var res = _conn.TestConnect(row.ToEndpoint());
            row.IsConnected = res.Ok;
            row.TestResult = (res.Ok ? "✓ " : "✗ ") + res.Message;
        }
        await SyncRealDevicesAsync();
    }

    /// <summary>
    /// 被检行「连接/断开」：真连该被检（按型号建驱动 ConnectAsync 探活，测完关闭）。
    /// 被检独立于共享设备组，不参与标准盒/PLC 组状态。
    /// </summary>
    /// <param name="row">被检连接行。</param>
    private async Task ToggleDutRowAsync(CommRowViewModel row)
    {
        if (row.IsConnected)
        {
            row.IsConnected = false;
            row.TestResult = "已断开";
            return;
        }

        row.TestResult = "正在连接…";
        var (ok, msg) = await _conn.TestDutAsync(_dut, row.ToEndpoint());
        row.IsConnected = ok;
        row.TestResult = (ok ? "✓ " : "✗ ") + msg;
    }

    /// <summary>
    /// 重算组状态（标准模块行连接状态即真实连接，无全局单例需对齐）。
    /// </summary>
    private Task SyncRealDevicesAsync()
    {
        RecomputeGroupStatus();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 依据各行连接状态重算标准模块组连接状态。
    /// </summary>
    private void RecomputeGroupStatus()
    {
        StandardConnected = StandardModules.Count > 0 && StandardModules.All(r => r.IsConnected);
    }

    /// <summary>
    /// 保存标准模块/被检连接配置到仓储并关闭窗口。
    /// </summary>
    [RelayCommand]
    private void Save()
    {
        var c = _store.Current;
        foreach (var r in StandardModules)
        {
            if (r.DeviceKey is not null)
            {
                c.StandardModules[r.DeviceKey] = r.ToEndpoint();
            }
        }

        c.Duts[_boardKey] = Duts.Select(d => d.ToEndpoint()).ToList();
        _store.Save();
        Status = "已保存";

        // 先关窗再弹 Toast：此时主窗恢复为活动窗口，Toast 落在主窗右上，不随配置窗一起消失
        RequestClose?.Invoke(this, EventArgs.Empty);
        AppToast.Success("配置已保存", "标准模块 / 被检连接参数已写入本地配置");
    }

    /// <summary>
    /// 取消并关闭窗口（不保存）。
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 通讯方式下拉项：枚举值 + 中文显示（网络/串口/USB）。
/// </summary>
public sealed record LinkOption(LinkType Value, string Label);

/// <summary>
/// 带中文标签的字符串下拉项（停止位/校验位）。
/// </summary>
public sealed record NamedOption(string Value, string Label);

/// <summary>
/// 单个设备连接行：通讯方式 + 对应字段（网络/串口/USB）+ 连接/断开。
/// </summary>
public partial class CommRowViewModel : ObservableObject
{
    /// <summary>
    /// 通讯方式枚举到中文标签的映射。
    /// </summary>
    private static readonly Dictionary<LinkType, string> LinkLabels = new()
    {
        [LinkType.Ethernet] = "网络",
        [LinkType.Serial] = "串口",
        [LinkType.Usb] = "USB",
    };

    /// <summary>
    /// 连接管理器（连通性测试）。
    /// </summary>
    private readonly ConnectionManager _conn;

    /// <summary>
    /// 防止 物理链路 ↔ 端口 互相匹配时递归。
    /// </summary>
    private bool _syncing;

    /// <summary>
    /// 串口下拉选项是否已扫描填充完成。填充前下拉对 SelectedValue 的清空属噪声，不能覆盖已存配置。
    /// </summary>
    private bool _optionsLoaded;

    /// <summary>
    /// 构造连接行：按端点回填各字段，并按允许的通讯方式限定下拉。
    /// </summary>
    /// <param name="name">显示名。</param>
    /// <param name="ep">初始通讯端点。</param>
    /// <param name="serialOptions">共享串口下拉选项。</param>
    /// <param name="usbOptions">共享 USB 下拉选项。</param>
    /// <param name="conn">连接管理器。</param>
    /// <param name="allowedLinks">允许的通讯方式。</param>
    /// <param name="showConnect">是否显示连接/断开按钮。</param>
    public CommRowViewModel(string name, CommEndpoint ep,
        ObservableCollection<DiscoveredSerial> serialOptions, ObservableCollection<DiscoveredUsb> usbOptions,
        ConnectionManager conn, LinkType[] allowedLinks, bool showConnect = true)
    {
        Name = name;
        _conn = conn;
        SerialOptions = serialOptions;
        UsbOptions = usbOptions;
        ShowConnect = showConnect;
        LinkOptions = allowedLinks.Select(l => new LinkOption(l, LinkLabels[l])).ToArray();
        LinkFixed = allowedLinks.Length <= 1;

        // 通讯方式定死时强制为允许的唯一类型，避免历史配置不一致
        Link = allowedLinks.Length == 1 ? allowedLinks[0] : ep.Link;
        SavedPhysicalLink = ep.PhysicalLink;
        Ip = ep.Ip;
        Port = ep.Port?.ToString(CultureInfo.InvariantCulture);
        PhysicalLink = ep.PhysicalLink;
        Baud = ep.Serial?.Baud ?? 9600;
        DataBits = ep.Serial?.DataBits ?? 8;
        StopBits = ep.Serial?.StopBits ?? "One";
        Parity = ep.Serial?.Parity ?? "None";
        Vid = ep.Vid?.ToString("X4") ?? (ep.Link == LinkType.Usb ? "2E19" : null);
        Pid = ep.Pid?.ToString("X4");
    }

    /// <summary>
    /// 显示名。
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 已存配置里的物理链路（构造时的原值）。串口下拉的 ItemsSource 是后台扫描后才填充的，
    /// 在此之前 WPF 匹配不到 SelectedValue 会把绑定源写回 null（配置「打不开就丢」的根因），
    /// 故保留原值，扫描完成后由 <see cref="ApplySavedSerialSelection"/> 回填。
    /// </summary>
    public string? SavedPhysicalLink { get; private set; }

    /// <summary>
    /// 串口下拉可选项（与父 VM 共享）。
    /// </summary>
    public ObservableCollection<DiscoveredSerial> SerialOptions { get; }

    /// <summary>
    /// USB 下拉可选项（与父 VM 共享）。
    /// </summary>
    public ObservableCollection<DiscoveredUsb> UsbOptions { get; }

    /// <summary>
    /// 允许的通讯方式下拉项。
    /// </summary>
    public LinkOption[] LinkOptions { get; }

    /// <summary>
    /// 通讯方式是否定死（只有一种允许方式）。
    /// </summary>
    public bool LinkFixed { get; }

    /// <summary>
    /// 通讯方式是否可改（标准盒/PLC 固定时为 false）。
    /// </summary>
    public bool LinkEditable => !LinkFixed;

    /// <summary>
    /// 是否显示「连接/断开」（被检在任务开始前不连接，故为 false）。
    /// </summary>
    public bool ShowConnect { get; }

    /// <summary>
    /// 由父 VM 注入：执行 连接/断开 并同步组状态。
    /// </summary>
    public Func<CommRowViewModel, Task>? ToggleHandler { get; set; }

    /// <summary>
    /// 设备实例键（标准模块行 = manifest ToolDevices 的 Key，如 DPSEX1/DPSEX2）；被检行为 null。
    /// </summary>
    public string? DeviceKey { get; init; }

    /// <summary>
    /// 标准模块实例键（manifest <c>ToolDevices[i].Key</c>，如 DPSEX1/DPSEX2），标准模块行有值，
    /// 供「一键连接」按 Key 取型号真连探活。
    /// </summary>
    public string? FixtureKey { get; init; }

    /// <summary>
    /// 波特率下拉选项。
    /// </summary>
    public int[] BaudOptions { get; } = [1200, 2400, 4800, 9600, 19200, 38400, 57600, 115200];

    /// <summary>
    /// 数据位下拉选项。
    /// </summary>
    public int[] DataBitsOptions { get; } = [5, 6, 7, 8];

    /// <summary>
    /// 停止位下拉选项（值 + 中文）。
    /// </summary>
    public NamedOption[] StopBitsOptions { get; } =
        [new("One", "1"), new("OnePointFive", "1.5"), new("Two", "2")];

    /// <summary>
    /// 校验位下拉选项（值 + 中文）。
    /// </summary>
    public NamedOption[] ParityOptions { get; } =
        [new("None", "无"), new("Odd", "奇"), new("Even", "偶"), new("Mark", "标记"), new("Space", "空格")];

    /// <summary>
    /// 当前通讯方式。
    /// </summary>
    [ObservableProperty] private LinkType _link;

    /// <summary>
    /// IP 地址（网络方式）。
    /// </summary>
    [ObservableProperty] private string? _ip;

    /// <summary>
    /// 端口号（网络方式）。
    /// </summary>
    [ObservableProperty] private string? _port;

    /// <summary>
    /// 物理链路（PortChain，串口/USB 方式）。
    /// </summary>
    [ObservableProperty] private string? _physicalLink;

    /// <summary>
    /// 串口 COM 号。
    /// </summary>
    [ObservableProperty] private string? _com;

    /// <summary>
    /// 波特率。
    /// </summary>
    [ObservableProperty] private int _baud;

    /// <summary>
    /// 数据位。
    /// </summary>
    [ObservableProperty] private int _dataBits;

    /// <summary>
    /// 停止位。
    /// </summary>
    [ObservableProperty] private string _stopBits = "One";

    /// <summary>
    /// 校验位。
    /// </summary>
    [ObservableProperty] private string _parity = "None";

    /// <summary>
    /// 当前选中的 USB 设备。
    /// </summary>
    [ObservableProperty] private DiscoveredUsb? _selectedUsb;

    /// <summary>
    /// USB VID（十六进制字符串）。
    /// </summary>
    [ObservableProperty] private string? _vid;

    /// <summary>
    /// USB PID（十六进制字符串）。
    /// </summary>
    [ObservableProperty] private string? _pid;

    /// <summary>
    /// 连通性测试结果文字。
    /// </summary>
    [ObservableProperty] private string _testResult = "";

    /// <summary>
    /// 是否已连接。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectLabel))]
    private bool _isConnected;

    /// <summary>
    /// 连接/断开 按钮文字。
    /// </summary>
    public string ConnectLabel => IsConnected ? "断开" : "连接";

    /// <summary>
    /// 当前是否为网络方式。
    /// </summary>
    public bool IsEthernet => Link == LinkType.Ethernet;

    /// <summary>
    /// 当前是否为串口方式。
    /// </summary>
    public bool IsSerial => Link == LinkType.Serial;

    /// <summary>
    /// 当前是否为 USB 方式。
    /// </summary>
    public bool IsUsb => Link == LinkType.Usb;

    /// <summary>
    /// 通讯方式变化时通知各方式判定属性刷新。
    /// </summary>
    /// <param name="value">新通讯方式。</param>
    partial void OnLinkChanged(LinkType value)
    {
        OnPropertyChanged(nameof(IsEthernet));
        OnPropertyChanged(nameof(IsSerial));
        OnPropertyChanged(nameof(IsUsb));
    }

    /// <summary>
    /// 串口：选物理链路 → 自动匹配 COM（与 <see cref="OnComChanged"/> 互相联动）。
    /// </summary>
    /// <param name="value">新物理链路。</param>
    partial void OnPhysicalLinkChanged(string? value)
    {
        if (_syncing || Link != LinkType.Serial) return;

        // 选项已就绪后的改动才是用户真实选择，记为新的已存值（含选「不选」清空）
        if (_optionsLoaded) SavedPhysicalLink = value;

        var hit = SerialOptions.FirstOrDefault(s => s.PortChain == value);
        if (hit is null) return;
        _syncing = true; Com = hit.Com; _syncing = false;
    }

    /// <summary>
    /// 串口：选 COM → 自动匹配物理链路（与 <see cref="OnPhysicalLinkChanged"/> 互相联动）。
    /// </summary>
    /// <param name="value">新 COM 号。</param>
    partial void OnComChanged(string? value)
    {
        if (_syncing) return;
        var hit = SerialOptions.FirstOrDefault(s => s.Com == value);
        if (hit is null) return;
        _syncing = true; PhysicalLink = hit.PortChain; _syncing = false;
    }

    /// <summary>
    /// USB：选设备 → 回填物理链路 + VID/PID。
    /// </summary>
    /// <param name="value">新选中的 USB 设备。</param>
    partial void OnSelectedUsbChanged(DiscoveredUsb? value)
    {
        if (value is null) return;
        PhysicalLink = value.PortChain;
        Vid = value.Vid.ToString("X4");
        Pid = value.Pid.ToString("X4");
    }

    /// <summary>
    /// 串口扫描完成后回填已存物理链路：下拉里有对应项则选中（并联动 COM），
    /// 没有（设备未插/换口）则补一条「未扫描到」占位项，保证反显不丢已存配置。
    /// <para>
    /// 必须「先清空再赋值」：ComboBox 绑定时下拉项还没扫描出来，匹配不到 SelectedValue 时它只是显示空、
    /// **不会把 null 写回源**，故此刻属性值与要回填的值相同，直接赋值被 <c>SetProperty</c> 判等吃掉、
    /// 不发通知，ComboBox 就一直空着（共享设备 Tab 默认展开才踩到；被检 Tab 懒加载绑定时列表已就绪）。
    /// </para>
    /// </summary>
    public void ApplySavedSerialSelection()
    {
        _optionsLoaded = true;
        if (Link != LinkType.Serial || string.IsNullOrWhiteSpace(SavedPhysicalLink))
        {
            return;
        }

        var saved = SavedPhysicalLink;
        var hit = SerialOptions.FirstOrDefault(s => s.PortChain == saved || s.Com == saved);
        if (hit is null)
        {
            hit = new DiscoveredSerial("", saved, "", $"{saved}（未扫描到）");
            SerialOptions.Add(hit);
        }

        _syncing = true;
        PhysicalLink = null;
        Com = null;
        _syncing = false;

        PhysicalLink = hit.PortChain;   // 联动设置 Com
        SavedPhysicalLink = hit.PortChain;
    }

    /// <summary>
    /// 「一键识别」命中后回填本行：先写探到的串口参数，再写物理链路（联动 COM 号并记为新的已存值）。
    /// 顺序不能颠倒——物理链路变化会触发联动，参数先落位可保证行显示与实际探通的参数一致。
    /// </summary>
    /// <param name="portChain">识别到的物理链路。</param>
    /// <param name="serial">识别时实际探通的串口参数（为 null 则保留原参数）。</param>
    public void ApplyIdentified(string portChain, SerialParams? serial)
    {
        if (serial is not null)
        {
            Baud = serial.Baud;
            DataBits = serial.DataBits;
            StopBits = serial.StopBits;
            Parity = serial.Parity;
        }

        PhysicalLink = portChain;
        SavedPhysicalLink = portChain;
    }

    /// <summary>
    /// 依当前通讯方式与字段构建通讯端点。
    /// </summary>
    /// <returns>通讯端点。</returns>
    public CommEndpoint ToEndpoint()
    {
        // 物理链路被下拉暂时清空时（扫描未完成）回落已存值，避免保存把配置清掉
        var link = string.IsNullOrWhiteSpace(PhysicalLink) ? SavedPhysicalLink : PhysicalLink;
        return Link switch
        {
            LinkType.Ethernet => CommEndpoint.OfEthernet(Ip ?? "", int.TryParse(Port, out var p) ? p : 0),
            LinkType.Serial => CommEndpoint.OfSerial(link ?? "", new SerialParams(Baud, DataBits, StopBits, Parity)),
            LinkType.Usb => CommEndpoint.OfUsb(link ?? "", ParseHex(Vid), ParseHex(Pid)),
            _ => new CommEndpoint(),
        };
    }

    /// <summary>
    /// 「连接/断开」命令：委托父 VM 的处理器。
    /// </summary>
    /// <returns>异步任务。</returns>
    [RelayCommand]
    private Task ToggleConnect()
    {
        return ToggleHandler?.Invoke(this) ?? Task.CompletedTask;
    }

    /// <summary>
    /// 解析十六进制字符串为整数，失败返回 0。
    /// </summary>
    /// <param name="s">十六进制字符串。</param>
    /// <returns>解析结果或 0。</returns>
    private static int ParseHex(string? s)
    {
        return int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v) ? v : 0;
    }
}
