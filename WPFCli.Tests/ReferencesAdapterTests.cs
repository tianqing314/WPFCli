using WPFCli.Engine;
using WPFCli.Models;
using Xunit;

namespace WPFCli.Tests;

/// <summary>
/// ReferencesAdapter 转换器测试：旧 Bots.TestBench 体系 → 新 PCBA 体系 的自动转换行为。
/// 使用构造的最小样本（真实数据在仓库 References\ConST221，测试保持自包含）。
/// </summary>
public sealed class ReferencesAdapterTests
{
    [Fact]
    public void Inject_when_references_missing_warns_and_continues()
    {
        var parent = CreateTempDirectory();
        try
        {
            var output = Path.Combine(parent, "out");
            Directory.CreateDirectory(output);
            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            opts.ReferencesRoot = Path.Combine(parent, "no-such-refs");

            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            Assert.False(result.Found);
            Assert.Empty(result.GeneratedFiles);
            Assert.Empty(result.RemovedFiles);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Inject_generates_interface_and_driver_from_uut()
    {
        var parent = CreateTempDirectory();
        try
        {
            var refs = Path.Combine(parent, "References", "ConST221");
            var uutDir = Path.Combine(refs, "Uut");
            Directory.CreateDirectory(uutDir);
            File.WriteAllText(Path.Combine(uutDir, "ConST221.cs"), SampleUutSource);
            var output = Path.Combine(parent, "out");
            Directory.CreateDirectory(output);

            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            var iface = Path.Combine(output, "src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut", "IConST221Dut.cs");
            var driver = Path.Combine(output, "src", "03.Devices", "TESTRIG.Devices", "Dut", "ConST221", "ConST221Dut.cs");
            Assert.True(File.Exists(iface), "接口文件未生成");
            Assert.True(File.Exists(driver), "驱动文件未生成");

            var ifaceText = File.ReadAllText(iface);
            // 13 个命令枚举（含中文名）+ SCPI 映射齐全
            Assert.Contains("CDP电源打开", ifaceText);
            Assert.Contains("FLASH正常写入擦除或其它操作", ifaceText);
            Assert.Contains("Task ExecuteAnyCommandNoResponseAsync(ConST221Command command, CancellationToken ct = default);", ifaceText);
            Assert.DoesNotContain("{dut}", ifaceText);

            var driverText = File.ReadAllText(driver);
            Assert.Contains("[DutDriver(\"ConST221\")]", driverText);
            Assert.Contains("public sealed class ConST221Dut : IConST221Dut", driverText);
            Assert.Contains("255:W:PCDP:1", driverText);
            Assert.Contains("using Xmas11.Comm.Devices;", driverText);
            Assert.DoesNotContain("{dut}", driverText);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Inject_generates_handlers_and_manifest_from_script_and_jig()
    {
        var parent = CreateTempDirectory();
        try
        {
            var refs = Path.Combine(parent, "References", "ConST221");
            Directory.CreateDirectory(Path.Combine(refs, "TestSteps"));
            Directory.CreateDirectory(Path.Combine(refs, "Jigs"));
            File.WriteAllText(Path.Combine(refs, "TestSteps", "ConST221_MainBoard_Auto.cs"), SampleScriptSource);
            File.WriteAllText(Path.Combine(refs, "Jigs", "ConST221_MainBoard_Auto.distributed.json"), SampleJigJson);
            var output = Path.Combine(parent, "out");
            Directory.CreateDirectory(output);

            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            var handler = Path.Combine(output, "src", "04.TestSteps", "TESTRIG.TestSteps", "ConST221", "ConST221_ControlBoard", "ConST221_ControlBoard.cs");
            var manifest = Path.Combine(output, "src", "05.Jigs", "TESTRIG.Jigs", "Manifests", "ConST221", "ConST221_ControlBoard.json");
            Assert.True(File.Exists(handler), "处理器文件未生成");
            Assert.True(File.Exists(manifest), "manifest 未生成");

            // 处理器：Ops + 每任务一个 handler，Kind/DeviceFamily 正确，判定语句转译正确
            var handlerText = File.ReadAllText(handler);
            Assert.Contains("internal sealed class ConST221Ops", handlerText);
            Assert.Contains("public sealed class PowerSourceTrackTestConST221Handler : IStepHandler", handlerText);
            Assert.Contains("public string Kind => \"PowerSourceTrackTest\";", handlerText);
            Assert.Contains("public string? DeviceFamily => \"ConST221\";", handlerText);
            Assert.Contains("pass &= op.Judge(\"VBACK指标\", VbackVolt, \"VbackVolt电压\", \"V\");", handlerText);
            Assert.Contains("currents = ConST221Ops.TrimCurrents(currents);", handlerText);
            Assert.DoesNotContain("TODO(自动转换)", handlerText);
            Assert.DoesNotContain("{dut}", handlerText);

            // manifest：Steps 数量、条件 Range 转换、Dut.Model
            var manifestText = File.ReadAllText(manifest);
            Assert.Contains("\"Key\": \"ConST221_ControlBoard\"", manifestText);
            Assert.Contains("\"Model\": \"ConST221\"", manifestText);
            Assert.Contains("\"Kind\": \"PowerSourceTrackTest\"", manifestText);
            Assert.Contains("\"Name\": \"VBACK指标\"", manifestText);
            Assert.Contains("\"Min\": 2.8", manifestText);
            Assert.Contains("\"Max\": 3.1", manifestText);
            Assert.Contains("\"Unit\": \"V\"", manifestText);
            Assert.Contains("\"Baud\": 19200", manifestText);
            Assert.Contains("\"StopBits\": \"Two\"", manifestText);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Inject_copies_dlls_and_links_csproj()
    {
        var parent = CreateTempDirectory();
        try
        {
            var refs = Path.Combine(parent, "References", "ConST221");
            var x11 = Path.Combine(refs, "Xmas11");
            Directory.CreateDirectory(x11);
            File.WriteAllBytes(Path.Combine(x11, "Xmas11.Comm.Devices.DPCEX.dll"), new byte[] { 0x4D, 0x5A });
            var output = Path.Combine(parent, "out");
            var csprojDir = Path.Combine(output, "src", "03.Devices", "TESTRIG.Devices");
            Directory.CreateDirectory(csprojDir);
            File.WriteAllText(Path.Combine(csprojDir, "TESTRIG.Devices.csproj"), """
                <Project>
                  <ItemGroup>
                    <Reference Include="Xmas11.Comm.Common"><HintPath>$(X11)\Xmas11.Comm.Common.dll</HintPath><Private>true</Private></Reference>
                  </ItemGroup>
                </Project>
                """);

            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            Assert.Equal(1, result.DllAdded);
            Assert.True(File.Exists(Path.Combine(output, "refdlls", "Xmas11.Comm.Devices.DPCEX.dll")));
            var csproj = File.ReadAllText(Path.Combine(csprojDir, "TESTRIG.Devices.csproj"));
            Assert.Contains("Xmas11.Comm.Devices.DPCEX", csproj);
            Assert.Contains("$(X11)\\Xmas11.Comm.Devices.DPCEX.dll", csproj);
            // 原有引用保留
            Assert.Contains("Xmas11.Comm.Common", csproj);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Inject_removes_placeholder_files_and_reports_todo()
    {
        var parent = CreateTempDirectory();
        try
        {
            var refs = Path.Combine(parent, "References", "ConST221");
            Directory.CreateDirectory(Path.Combine(refs, "Uut"));
            File.WriteAllText(Path.Combine(refs, "Uut", "ConST221.cs"), SampleUutSource);
            var output = Path.Combine(parent, "out");
            // 模拟模板内置占位文件
            var phIfaceDir = Path.Combine(output, "src", "03.Devices", "TESTRIG.Devices.Abstractions", "Dut");
            var phDutDir = Path.Combine(output, "src", "03.Devices", "TESTRIG.Devices", "Dut", "ConST171");
            Directory.CreateDirectory(phIfaceDir);
            Directory.CreateDirectory(phDutDir);
            File.WriteAllText(Path.Combine(phIfaceDir, "IConST171Dut.cs"), "placeholder");
            File.WriteAllText(Path.Combine(phDutDir, "ConST171Dut.cs"), "placeholder");

            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            Assert.False(File.Exists(Path.Combine(phIfaceDir, "IConST171Dut.cs")));
            Assert.False(Directory.Exists(phDutDir));
            Assert.Contains(result.RemovedFiles, p => p.Contains("IConST171Dut.cs"));
            // 报告文件生成（UTF-8 带 BOM，可直接文本读取）
            var report = Path.Combine(output, ReferencesAdapter.ReportFileName);
            Assert.True(File.Exists(report));
            Assert.Contains("ConST221", File.ReadAllText(report));
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void Translate_body_marks_unmappable_statements_as_todo()
    {
        var parent = CreateTempDirectory();
        try
        {
            var refs = Path.Combine(parent, "References", "ConST221");
            Directory.CreateDirectory(Path.Combine(refs, "TestSteps"));
            Directory.CreateDirectory(Path.Combine(refs, "Jigs"));
            // 脚本含一条无法映射的语句
            var script = SampleScriptSource.Replace(
                "ScriptHelper.Thread_Sleep(new ScriptHelperKVP(10 * 1000));",
                "SomeUnknownHelper.DoThing();");
            File.WriteAllText(Path.Combine(refs, "TestSteps", "ConST221_MainBoard_Auto.cs"), script);
            File.WriteAllText(Path.Combine(refs, "Jigs", "ConST221_MainBoard_Auto.distributed.json"), SampleJigJson);
            var output = Path.Combine(parent, "out");
            Directory.CreateDirectory(output);

            var opts = CreateOptions(Path.Combine(parent, "Template"), parent);
            var result = ReferencesAdapter.Inject(opts, "ConST221", output);

            Assert.NotEmpty(result.TodoItems);
            var handler = File.ReadAllText(Path.Combine(output, "src", "04.TestSteps", "TESTRIG.TestSteps", "ConST221", "ConST221_ControlBoard", "ConST221_ControlBoard.cs"));
            Assert.Contains("// TODO(自动转换): SomeUnknownHelper.DoThing();", handler);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    // ===== 样本 =====

    private static BuildOptions CreateOptions(string templateRoot, string workspaceRoot)
    {
        Directory.CreateDirectory(templateRoot);
        return new BuildOptions
        {
            ProjectCode = "PT01",
            Template = new TemplateConfig { Placeholder = "PCBA" },
            TemplatePath = templateRoot,
            BusinessTemplate = new TemplateConfig { DutPlaceholder = "ConST171" },
            BusinessTemplatePath = Path.Combine(templateRoot, "Dynamic"),
            OutputDir = Path.Combine(workspaceRoot, "out"),
            ReferencesRoot = Path.Combine(workspaceRoot, "References"),
        };
    }

    private const string SampleUutSource = """
        using Xmas11.Comm.Data.Common;
        using Xmas11.Comm.Devices.DPG2;

        namespace Bots.TestBench.Device
        {
            public class ConST221_2
            {
                public DPG2SCPI DPG2 { get { return null; } }
                public ScriptHelperKVP ExecuteAnyCommand_NoResponse(SimpleCommandEnum sce)
                {
                    iResponse res = DPG2.ExecuteAnyCommand_NoResponse(SimpleCommands[sce]);
                    return new ScriptHelperKVP(res.IsCorrect ? "ok" : "fail", res.IsCorrect);
                }
                public enum SimpleCommandEnum
                {
                    CDP电源打开, CDP电源关闭, 液晶屏电源打开, 液晶屏电源关闭, 触摸屏电源打开,
                    触摸屏电源关闭, FRAM电源打开, FRAM电源关闭, FLASH电源打开, FLASH电源关闭,
                    能够写入读取时间即通过, 铁电正常写入擦除或其它操作, FLASH正常写入擦除或其它操作,
                }
                public Dictionary<SimpleCommandEnum, string> SimpleCommands = new Dictionary<SimpleCommandEnum, string>
                {
                    {SimpleCommandEnum.CDP电源打开,"255:W:PCDP:1"},
                    {SimpleCommandEnum.CDP电源关闭,"255:W:PCDP:0"},
                    {SimpleCommandEnum.液晶屏电源打开,"255:W:PLCD:1"},
                    {SimpleCommandEnum.液晶屏电源关闭,"255:W:PLCD:0"},
                    {SimpleCommandEnum.触摸屏电源打开,"255:W:PTSP:1"},
                    {SimpleCommandEnum.触摸屏电源关闭,"255:W:PTSP:0"},
                    {SimpleCommandEnum.FRAM电源打开,"255:W:PFRAM:1"},
                    {SimpleCommandEnum.FRAM电源关闭,"255:W:PFRAM:0"},
                    {SimpleCommandEnum.FLASH电源打开,"255:W:PFLASH:1"},
                    {SimpleCommandEnum.FLASH电源关闭,"255:W:PFLASH:0"},
                    {SimpleCommandEnum.能够写入读取时间即通过,"255:W:TRTC"},
                    {SimpleCommandEnum.铁电正常写入擦除或其它操作,"255:W:TFRAM"},
                    {SimpleCommandEnum.FLASH正常写入擦除或其它操作,"255:W:TFLASH"},
                };
            }
        }
        """;

    private const string SampleScriptSource = """
        class ConST221_MainBoard_Auto
        {
            public DecorationClass GetDC(AutoTestItem ati) { return null; }
            public dynamic BenchPreparation(dynamic item)
            {
                var DC = GetDC(item as AutoTestItem);
                ScriptHelper.SetDisplayer(item as AutoTestItem);
                return ScriptHelper.WatchAndProcessIntergrade(false, () =>
                {
                    ScriptHelper.AddNewRange(DC.DSTB.NetSwitchACloseAllChannels());
                    ScriptHelper.AddNewRange(DC.DSTB.NetSwitchBCloseAllChannels());
                    ScriptHelper.AddNewRange(DC.DSTB.NetSwitchCCloseAllChannels(false));
                    ScriptHelper.Thread_Sleep(new ScriptHelperKVP(10 * 1000));
                });
            }
            public dynamic PowerSourceTrackTest(dynamic item)
            {
                var DC = GetDC(item as AutoTestItem);
                ScriptHelper.SetDisplayer(item as AutoTestItem);
                return ScriptHelper.WatchAndProcessIntergrade(false, () =>
                {
                    var vbackCondition = DC.Conditions.FirstOrDefault(o => o.Name == "VBACK指标") as RangeCondition;
                    var closeVolt = DC.Conditions.FirstOrDefault(o => o.Name == "关闭电源") as RangeCondition;
                    ScriptHelper.AddNewRange(DC.P22.ExecuteAnyCommand_NoResponse(SimpleCommandEnum.CDP电源关闭));
                    ScriptHelper.AddNewIsRangeJudge(DC.DSTB.GetVoltageMeasureValue(5, out double VbackVolt), vbackCondition.Lower, vbackCondition.Upper);
                    List<ScriptHelperKVP> currents = new List<ScriptHelperKVP>();
                    while (currents.Count < 30)
                    {
                        ScriptHelperKVP scriptKvp = DC.DSTB.GetCurrentMeasureValue(false, 1, out double value);
                        if (value != double.NaN)
                            currents.Add(scriptKvp);
                        Thread.Sleep(500);
                    }
                    currents = ScriptHelperKVP.TrimCurrents(currents);
                    ScriptHelper.AddNewIsRangeJudge(new ScriptHelperKVP(currents.FirstOrDefault().Content) { JudgeObject = currents.Average(o => double.Parse(o.JudgeObject.ToString())) }, closeVolt.Lower, closeVolt.Upper);
                });
            }
            public dynamic ConsumeTest(dynamic item)
            {
                var DC = GetDC(item as AutoTestItem);
                ScriptHelper.SetDisplayer(item as AutoTestItem);
                return ScriptHelper.WatchAndProcessIntergrade(false, () =>
                {
                    var vbackOpenCondition = DC.Conditions.FirstOrDefault(o => o.Name == "功耗范围(打开)") as RangeCondition;
                    currents = new List<ScriptHelperKVP>();
                    while (currents.Count < 30)
                    {
                        ScriptHelperKVP scriptKvp = DC.DSTB.GetCurrentMeasureValue(false, 1, out double value);
                        if (value != double.NaN)
                            currents.Add(scriptKvp);
                        Thread.Sleep(500);
                    }
                    currents = ScriptHelperKVP.TrimCurrents(currents);
                    ScriptHelper.AddNewIsRangeJudge(new ScriptHelperKVP(currents.FirstOrDefault().Content) { JudgeObject = currents.Average(o => double.Parse(o.JudgeObject.ToString())) }, vbackOpenCondition.Lower, vbackOpenCondition.Upper);
                });
            }
        }
        """;

    private const string SampleJigJson = """
        {
          "GUID": "B9551499-CBD2-4c98-8A2A-37C18C601339",
          "Name": "ConST221系统板动态测试",
          "View": "TestBench_SelfCheckTaskRunView",
          "ScriptFile": "ConST221_MainBoard_Auto.cs",
          "RefAssemblies": "Bots.TestBench.Device.Base.dll,Bots.TestBench.Model.Task.dll",
          "Type": { "Type": 14, "Categories": "ConST221HT", "IsManualConfig": false, "TestCategoriesItems": [] },
          "Devices": [
            {
              "$type": "Bots.TestBench.Device.ConST221_2, Bots.TestBench.Device.ConST221",
              "DeviceKey": "P22",
              "DeviceName": "221主板针床",
              "DeviceMode": "221PCB",
              "DeviceType": "DUT",
              "CommConfigs": [
                { "$type": "Bots.TestBench.Device.Base.Comm.SerialPortConfig, Bots.TestBench.Device.Base", "Bauds": 19200, "Name": "Board", "StopBits": "Two", "Parity": "None" }
              ]
            }
          ],
          "TaskCollection": [
            {
              "$type": "Bots.TestBench.Model.Task.AutoTestItem, Bots.TestBench.Model.Task",
              "Location": { "Package": "ConST221_MainBoard_Auto", "Entry": "BenchPreparation" },
              "SortIdentifier": 1, "Categories": "ConST221HT", "Name": "工装准备",
              "TestDesc": "网络继电器A切档1：CHA通道3.3V供电", "HandleDesc": "",
              "Parameters": [], "Conditions": [], "GUID": "6a4b0baf-aec5-406f-8212-925544377edd"
            },
            {
              "$type": "Bots.TestBench.Model.Task.AutoTestItem, Bots.TestBench.Model.Task",
              "Location": { "Package": "ConST221_MainBoard_Auto", "Entry": "PowerSourceTrackTest" },
              "SortIdentifier": 3, "Categories": "ConST221HT", "Name": "电源轨测试",
              "TestDesc": "读取6通道电压", "HandleDesc": "",
              "Parameters": [], "Conditions": [
                { "$type": "Bots.TestBench.Model.Scripts.RangeCondition, Bots.TestBench.Model.Scripts", "Name": "VBACK指标", "Lower": 2.8, "Upper": 3.1, "Unit": "V" },
                { "$type": "Bots.TestBench.Model.Scripts.RangeCondition, Bots.TestBench.Model.Scripts", "Name": "关闭电源", "Lower": 0, "Upper": 0.1, "Unit": "V" }
              ], "GUID": "3fa5bca4-3df9-4eb6-b991-667b4c63c5ef"
            },
            {
              "$type": "Bots.TestBench.Model.Task.AutoTestItem, Bots.TestBench.Model.Task",
              "Location": { "Package": "ConST221_MainBoard_Auto", "Entry": "ConsumeTest" },
              "SortIdentifier": 7, "Categories": "ConST221HT", "Name": "功耗测试",
              "TestDesc": "读取电流", "HandleDesc": "",
              "Parameters": [], "Conditions": [
                { "$type": "Bots.TestBench.Model.Scripts.RangeCondition, Bots.TestBench.Model.Scripts", "Name": "功耗范围(打开)", "Lower": 29, "Upper": 31, "Unit": "mA" }
              ], "GUID": "0fe594bf-d6b3-4b4f-8249-e836e39cac6c"
            }
          ]
        }
        """;

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "testrig-refs-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
