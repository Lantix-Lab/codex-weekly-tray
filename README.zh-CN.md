# Codex Weekly Tray

[English](README.md) | [简体中文](README.zh-CN.md)

一个小巧的 Windows 托盘程序，以饼图显示 Codex 每周和 5 小时使用限额的剩余比例。

这是一个非官方社区项目，与 OpenAI 不存在隶属、认可或支持关系。

## 图标行为

- 同一个程序会为每个实际存在的限额窗口创建独立的托盘图标。
- 每周窗口使用一个图标；仅当接口返回 5 小时窗口时，才显示第二个图标。
- 如果账户只返回一个受支持的窗口，只显示一个图标属于正常情况。
- 蓝色外圈表示每周图标。
- 紫色外圈表示 5 小时图标。
- 托盘图标顺序由 Windows 管理，因此不使用左右位置区分窗口。
- 彩色区域表示剩余比例。
- 已使用部分从 12 点方向开始，顺时针移除彩色区域。
- 剩余量高于 50% 时显示绿色。
- 剩余量为 21% 至 50% 时显示黄色。
- 剩余量为 0% 至 20% 时显示红色。
- 已使用区域显示为深灰色。
- 数据不可用时显示带斜线的灰色圆形。
- 图标不包含文字或数字。

内部扇形颜色表示剩余量级，外圈颜色表示窗口类型。每个图标的悬停提示也会标明对应窗口，并显示准确的剩余百分比和重置时间。两个图标共享同一个菜单、刷新定时器、App Server 连接和程序进程。每次刷新只发送一次 `account/rateLimits/read` 请求，并更新接口返回的所有窗口；显示两个图标不会使请求频率翻倍。

## 支持的窗口

- 300 分钟窗口显示为 5 小时图标。
- 10,080 分钟窗口显示为每周图标。
- 如果接口同时返回两个窗口，则同时显示两个图标。
- 如果接口只返回一个受支持的窗口，则只显示对应图标。

程序根据 `windowDurationMins` 识别窗口类型，不根据账户套餐名称推断。

## 数据来源与隐私

程序通过本机 `codex app-server` 的 `account/rateLimits/read` 方法读取数据，并复用现有的 Codex 登录状态。

- 不抓取网页。
- 不读取或保存密码。
- 不保存访问令牌。
- 不向第三方发送数据。

协议说明参见 [OpenAI Codex App Server 文档](https://learn.chatgpt.com/docs/app-server)。

## 用量影响

运行本程序不会消耗模型推理 Token，也不会减少 Codex 的任何使用额度。程序只调用 `account/rateLimits/read` 读取账户元数据，从不启动 Codex 对话或模型回合，也不调用 `thread/start` 或 `turn/start`。

可选刷新间隔只会产生少量账户状态请求，本机 CPU、内存和网络开销可以忽略。Codex App Server 自身可能进行轻量的元数据刷新，但本程序不会请求模型推理。

## 编码规则

所有源代码、脚本、配置文件和英文文档均限制为 7-bit ASCII。本地化文档可以使用无 BOM 的 UTF-8。构建过程会运行 `verify-ascii.ps1`，任何文件违反其编码规则都会导致构建失败。

## 系统要求

- Windows 10 或 Windows 11。
- 已安装并登录 Codex 桌面应用或 Codex CLI。
- .NET Framework 4.5 或更高版本。

如果无法通过 `PATH` 找到 `codex.exe`，程序还会检查 Codex 桌面应用的默认安装目录。也可以通过 `CODEX_CLI_PATH` 环境变量明确指定完整路径。

## 构建

在 PowerShell 中运行：

```powershell
.\build.ps1
```

输出文件：

```text
dist\CodexWeeklyTray.exe
```

构建过程使用 Windows 自带的 C# 编译器，不下载 NuGet 包，并默认运行离线测试。

Windows 会在程序运行时锁定可执行文件，因此重新构建前应通过托盘菜单退出正在运行的程序。

## 分发规则

本仓库只分发源代码，不通过 GitHub Releases 或 Actions 构建产物发布预编译的可执行文件。用户需要克隆或下载仓库，并在本机运行 `build.ps1` 完成构建。

## 运行与测试

启动程序：

```powershell
.\dist\CodexWeeklyTray.exe
```

运行离线测试：

```powershell
.\test.ps1
```

额外使用当前已登录账户进行联调测试：

```powershell
.\test.ps1 -Live
```

单独验证编码规则：

```powershell
.\verify-ascii.ps1
```

## 托盘菜单

- **Refresh now**：立即重新读取使用额度。
- **Refresh interval**：选择 1、5、15 或 30 分钟。默认值为 1 分钟，所选档位会为当前 Windows 用户保存。
- **Open Codex usage page**：在默认浏览器中打开用量页面。
- **Start with Windows**：在当前用户的 Windows Startup 文件夹中添加或移除快捷方式，默认关闭。
- **Exit**：关闭程序及其 App Server 子进程。

双击任一托盘图标都会立即刷新两个图标。切换刷新间隔时也会立即刷新一次，后续按所选间隔自动刷新。

所选间隔保存在 `HKCU\Software\CodexWeeklyTray` 下的 `RefreshIntervalMinutes` 值中；该位置不保存账户凭据或用量历史。

在至少成功读取一次之后，前两次连续刷新失败会保留最后一次成功的图形，并在菜单文字和悬停提示中标记为 `stale`。第三次连续失败才切换为不可用图形。任一次刷新成功都会立即清零失败计数。如果程序启动后从未读取到有效数据，由于没有历史结果可显示，第一次失败仍会保持不可用状态。

启动快捷方式以 `CodexWeeklyTray.lnk` 的名称保存在 `shell:startup` 打开的文件夹中。使用当前用户 `Run` 注册表项的旧版本会自动迁移：程序会先创建并验证快捷方式，成功后才删除旧注册表值。

## 项目结构

```text
src/CodexWeeklyTray/   程序源码
tests/                 离线测试及可选的真实账户联调测试
.github/workflows/     GitHub Actions 构建配置
README.zh-CN.md        简体中文文档
build.ps1              本地构建入口
test.ps1               测试入口
verify-ascii.ps1       ASCII 与 UTF-8 编码检查
```

## 许可证

本项目采用 [MIT License](LICENSE)。
