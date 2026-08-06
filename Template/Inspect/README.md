# Inspect 出厂检验模板

出厂检验业务模板（TESTRIG 出厂检验平台），内置 TemplateUUT 出厂占位演示。

## 与动态工装模板（Dynamic）的差异

| 维度 | Dynamic 动态测试 | Inspect 出厂检验 |
|---|---|---|
| 自动化 | PLC 全自动上下料 / 工位 A/B/C / 自动循环 | 无 PLC，操作员人工上下料，手动单次 |
| 连接配置 | 含 PLC（国锐）连接行 | 无 PLC 行 |
| 数据查询 | 含"工位"列 | 无"工位"列 |
| 专属 UI | 自动化块 | **证书/合格证**（按通过的测试记录生成合格证 HTML，浏览器预览/打印） |
| Manifest | 一变体一清单 | 同上；出厂处理器放 `TestSteps/<设备族>/`，通用放 `Shared/` |

## 内置占位（TemplateUUT_Inspect）

`Manifests/TemplateUUT/TemplateUUT_Inspect.json` 演示一条完整出厂流程：

1. `InspectPrep` 出厂检验准备（Auto）
2. `AppearanceInspect` 外观检查（Auto）
3. `AccessoryInspect` 附件检查（Auto）
4. `FunctionRecheck` 功能复检（Auto）
5. `InspectFinish` 出厂检验结束（Auto）

证书/合格证在菜单「功能 → 证书 / 合格证」：列出本型号已通过的测试记录，
选中后「生成合格证」渲染 HTML（含产品型号/SN/检验项表/结论/检验章位），保存到 `AppContext.BaseDirectory/Certificates/` 并打开浏览器预览打印。

## 构建

```
testrig-cli --biz inspect --code <代号> [--dut <被检>]
```
