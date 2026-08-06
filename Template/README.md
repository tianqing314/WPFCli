# TESTRIG 动态测试平台（模板）

基于 .NET 8 / WPF / MaterialDesign 的产线 TESTRIG 测试平台，采用**数据驱动 + 分层架构**设计。
本模板从完整 TESTRIG 项目最小化提取，仅保留 **TemplateUUT（TemplateUUT 控制板）** 一款被检，默认仿真模式，开箱即编译运行。

## 目录结构

```
Template/
  Common/                          公共骨架（跨业务共享）
    src/01.Core/                   引擎契约 + 测试引擎
      TESTRIG.Core.Abstractions/      测试流程契约（ITestStep / ITestEngine / JigManifest …）
      TESTRIG.Core.Engine/            测试引擎实现（顺序执行、异常收尾、结果落库）
    src/02.Infrastructure/         基础设施（SQLite 结果库 / MySQL 远程同步 / Serilog 日志）
    src/03.Devices/                设备层
      TESTRIG.Devices.Abstractions/   设备接口契约（被检 / 标准盒 / PLC / 蓝牙 / 环境温度 …）
      TESTRIG.Devices/                设备驱动实现（TemplateUUT 被检 + 标准盒 + PLC + 扫描器）
      TESTRIG.Devices.BleWin/         Win10 蓝牙扫描（WinRT 广播监听）
  Dynamic/                         动态工装业务
    TESTRIG.sln                       解决方案（跨引用 Common + Dynamic 共 12 个项目）
    src/04.TestSteps/              测试项处理器（IStepHandler 反射注册，TemplateUUT 专属测试项）
    src/05.Jigs/                   针床 Manifest（JSON 声明被检/号位/测试项，运行时加载）
    src/06.Automation/             自动化流程编排
    src/07.UI/                     WPF 界面（主窗 / 测试运行 / 连接配置 / 维护页）
    src/08.App/                    启动项目（DI 组装 + WPF 宿主）
    src/09.Updater/                升级器（net48，独立于主程序）
```

## 构建与运行

```bash
cd Template/Dynamic
dotnet restore TESTRIG.sln
dotnet build TESTRIG.sln -c Debug
# 启动（仿真模式）
dotnet run --project src/08.App/TESTRIG.App
```

## 仿真模式

`appsettings.json` 中 `Pcba:Hardware:UseReal` 默认为 `false`（仿真）：
- 标准盒 / PLC 走仿真驱动，无需真实硬件。
- TemplateUUT 被检无仿真变体，`UseReal=false` 时仍加载真机驱动类（反射注册），但不会主动连接——
  启动与 UI 正常，仅在点「开始测试」时尝试真机连接并按异常收尾（不崩溃）。
- 切换真机：将 `UseReal` 改为 `true`，或设环境变量 `TESTRIG_REAL_HARDWARE=1`。

## 新增被检（加板）流程

1. **设备驱动**：在 `Common/src/03.Devices/TESTRIG.Devices/Dut/<型号>/` 添加驱动类，打 `[DutDriver("型号")]` 特性即可自动注册（无需手写 DI）。
2. **测试项**：在 `Dynamic/src/04.TestSteps/TESTRIG.TestSteps/<型号>/` 添加 `IStepHandler` 实现，反射自动注册。
3. **针床清单**：在 `Dynamic/src/05.Jigs/TESTRIG.Jigs/Manifests/<型号>/` 添加一份 JSON（声明被检型号、号位、测试项列表）。

以上三步即可，框架代码无需改动。

## 包管理

采用 Central Package Management（CPM），所有 NuGet 包版本统一在 `Common/Directory.Packages.props` 中声明，`Dynamic/` 通过 Import 复用。
