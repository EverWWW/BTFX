# Gait Intelligent Analysis System (BTFX)

## 开始时间
2026年1月8日

## 项目简介
Gait Intelligent Analysis System (BTFX) 是一个基于 .NET 10 和 WPF 开发的步态智能分析系统。该系统旨在通过计算机视觉和通过传感器数据分析患者的步态参数，辅助医生进行诊断和康复评估。

## 技术栈
- **开发框架**: .NET 10
- **UI 框架**: WPF (Windows Presentation Foundation)
- **架构模式**: MVVM (Model-View-ViewModel)
- **数据存储**: SQLite (Via EF Core or direct access)
- **依赖注入**: Microsoft.Extensions.DependencyInjection

## 核心功能模块
1. **患者管理 (Patient Management)**: 患者信息的录入、查询、编辑和管理。
2. **测量评估 (Measurement)**: 连接设备进行步态数据采集与实时预览。
3. **数据管理 (Data Management)**: 历史测量数据的存储、查询与导出。
4. **报告生成 (Report)**: 基于测量数据生成专业的分析报告。
5. **系统设置 (Settings)**: 语言切换、主题设置、数据库配置等。

## 环境要求
- Windows 10/11
- .NET 10 Runtime 或 SDK

## 快速开始
1. 克隆代码仓库。
2. 使用 Visual Studio 2022 或更高版本打开解决方案。
3. 还原 NuGet 包。
4. 启动项目。

## 目录结构
- `BTFX`: 主应用程序项目。
- `BTFX/Models`: 数据模型。
- `BTFX/ViewModels`: 视图模型。
- `BTFX/Views`: UI 视图。
- `BTFX/Services`: 业务逻辑服务。
- `BTFX/Resources`: 资源文件（样式、语言包等）。
- `GIAS`: 工具类库目录 (Logging, Communication, etc.)。

## 开发规范
请参考 `BTFX/Development` 目录下的文档，了解具体的开发规范和设计文档。
