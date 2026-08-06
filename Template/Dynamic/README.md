# Dynamic 动态工装模板（businessType=dynamic，预留）

> ⚠ **预留模板，暂不可用**（`template.config.json` 中 `disabled: true`，向导中显示但不可选择）。

## 预留内容规划（后续填充）

- **动态工装**：DynamicStandardTestBench 等动态工装设备驱动
- 通用动态工装测试逻辑

## 启用方式

业务开发就绪后，将 `template.config.json` 中的 `disabled` 改为 `false` 即可在向导中可选。

## 框架说明

- 启用后应用层统一 `net8.0-windows10.0.19041.0`（.NET 8），与 Common 骨架一致
