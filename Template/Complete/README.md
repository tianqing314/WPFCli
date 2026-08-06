# Complete 组件测试模板

组件测试业务模板（TESTRIG 组件测试平台），内置 TemplateUUT 组件占位演示。

## 与动态工装模板（Dynamic）的差异

| 维度 | Dynamic 动态测试 | Complete 组件测试 |
|---|---|---|
| 自动化 | PLC 全自动上下料 / 工位 A/B/C / 自动循环 | 无 PLC，操作员人工上下料，手动单次 |
| 连接配置 | 含 PLC（国锐）连接行 | 无 PLC 行 |
| 数据查询 | 含"工位"列 | 无"工位"列 |
| 专属 UI | 自动化块 | **工装/治具管理**（台账查看/增删/保存，持久化 `tooling.json`） |
| Manifest | 一变体一清单 | 同上；组件处理器放 `TestSteps/<设备族>/`，通用放 `Shared/` |

## 内置占位（TemplateUUT_Complete）

`Manifests/TemplateUUT/TemplateUUT_Complete.json` 演示一条完整组件流程：

1. `CompletePrep` 组件准备（Auto）
2. `AssemblyCheck` 组件装配检查（Auto，Settings 列检查点）
3. `FunctionCheck` 组件功能检查（Auto）
4. `ToolingBind` 工装绑定（Auto，记录本次使用工装 SN）
5. `CompleteFinish` 组件结束（Auto）

工装/治具台账在菜单「功能 → 工装 / 治具管理」维护，数据落 `AppContext.BaseDirectory/tooling.json`。

## 构建

```
testrig-cli --biz complete --code <代号> [--dut <被检>]
```
