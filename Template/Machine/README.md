# Machine 整机测试模板

整机测试业务模板（TESTRIG 整机测试平台），内置 TemplateUUT 整机占位演示。

## 与动态工装模板（Dynamic）的差异

| 维度 | Dynamic 动态测试 | Machine 整机测试 |
|---|---|---|
| 自动化 | PLC 全自动上下料 / 工位 A/B/C / 自动循环 | 无 PLC，操作员人工上下料，手动单次 |
| 连接配置 | 含 PLC（国锐）连接行 | 无 PLC 行 |
| 数据查询 | 含"工位"列 | 无"工位"列 |
| 人工确认步 | 无（全自动） | ManualStep：LCD/扬声器等人工观察项弹确认框（OK/NG，可选超时） |
| 温控过程项 | 无 | Process 步：高温炉等分钟级过程，实时曲线 + 条件等待（ProcessWaiter） |
| Manifest | 一变体一清单（<产品代号>_Machine） | 同上；整机专属处理器放 `TestSteps/<设备族>/`，通用放 `Shared/` |

## 内置占位（TemplateUUT_Machine）

`Manifests/TemplateUUT/TemplateUUT_Machine.json` 演示一条完整整机流程：

1. `MachinePrep` 整机准备（Auto）
2. `ManualLcdCheck` LCD 显示检查（**Manual**，弹确认框，60s 超时）
3. `ManualSpeakerCheck` 扬声器声音检查（**Manual**）
4. `OvenWarmup` 高温炉升温（**Process**，ProcessWaiter 轮询炉温至 660℃±5，实时曲线）
5. `MachineFinish` 整机结束（Auto）

真实整机产品接入：建 `<产品代号>_Machine` 清单（一变体一清单），
整机处理器放 `TestSteps/<产品代号>/`，通用处理器放 `Shared/` 复用。

## 构建

```
testrig-cli --biz machine --code <代号> [--dut <被检>]
```
