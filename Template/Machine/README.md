# Machine 整机测试通用模板（businessType=machine）

> 整机测试工装完整业务模板。生成时与 `../Common/`（公共基础层，.NET 8）合并拷贝：
> 同名文件覆盖 Common，不同名文件共存。

## 目录职责

- `src/07.UI/TESTRIG.UI.Shared/`：整机前端业务逻辑、ViewModel、Views、主题和窗口服务
- `src/08.App/TESTRIG.App/`：WPF 启动项目、应用配置、资源和随应用发布的文档
- `TESTRIG.sln`：包含 Common 公共项目与本目录前端项目的完整整机解决方案
- 其他整机专属任务 JSON、脚本和资源放在本目录对应的相对路径

## 目录约定

- 与 Common 同名文件放**相同相对路径**即可覆盖
- 新增文件放任意相对路径，拷贝时自动追加
- 本目录的 `template.config.json` 不会被拷贝进生成产物

## 框架说明

- 应用层统一 `net8.0-windows10.0.19041.0`（.NET 8），与 Common 骨架一致
- 目标框架、配置、占位符等全局字段由 `../template.config.json` 统一管理，本模板不覆盖
