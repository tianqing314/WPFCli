# Aging 老化模板（businessType=aging，disabled）

当前仅保留配置和业务规划说明。老化测试需要先提供自己的前端业务逻辑、WPF UI、App 和完整解决方案，完成后再将 `template.config.json` 中的 `disabled` 改为 `false`。

> 老化测试工装业务模板。生成时与 `../Common/`（公共骨架，.NET 8）合并拷贝：
> 同名文件覆盖 Common，不同名文件共存。

## 本目录放置内容（按需填充）

- **老化任务**：AgingPosition 位置控制等老化专用逻辑
- **老化脚本骨架**：长时间运行/循环测试步骤

## 目录约定

- 与 Common 同名文件放**相同相对路径**即可覆盖（如 `src/07.UI/.../MainWindow.xaml`）
- 新增文件放任意相对路径，拷贝时自动追加
- 本目录的 `template.config.json` 不会被拷贝进生成产物

## 框架说明

- 应用层统一 `net8.0-windows10.0.19041.0`（.NET 8），与 Common 骨架一致
- 目标框架、配置、占位符等全局字段由 `../template.config.json` 统一管理，本模板不覆盖
