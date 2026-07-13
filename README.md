# MobaXtermPasswordRecovery

一个带中文图形界面的 MobaXterm 凭据恢复工具，支持安装版注册表配置和便携版 `MobaXterm.ini`。下载后可以直接双击运行，不需要安装 .NET 或使用命令行。

本项目基于 [h0ny/MobaXtermDecryptor](https://github.com/h0ny/MobaXtermDecryptor) 修改并继续维护。原项目版权归原作者所有，本项目遵循 MIT License。

## 改进内容

- 兼容缺少 `[Credentials]` 或 `[Passwords]` 节的配置文件。
- 允许 MobaXterm 的 `[Macros]` 等配置节包含重复键。
- 展开反射调用异常，输出实际的异常类型和原因。
- 使用新的项目及可执行文件名称 `MobaXtermPasswordRecovery`。
- 提供适合普通用户使用的 Windows 图形界面。
- 支持浏览和自动查找 `MobaXterm.ini`。
- 结果默认隐藏密码，可按需显示或复制。
- 自包含单文件 EXE，无需另外安装运行环境。

## 使用限制

本工具仅用于恢复你本人拥有或已获明确授权管理的凭据。便携版的 Master Password 数据可能受 Windows DPAPI 保护，因此通常需要在保存密码时使用的原电脑、原 Windows 用户环境中运行。

运行结果可能包含明文密码。请勿截图、上传、共享或将输出保存到公共位置。

## 下载和使用

从 [Releases](https://github.com/MushGrowth/MobaXtermPasswordRecovery/releases) 下载最新版：

```text
MobaXtermPasswordRecovery.exe
```

双击 EXE 后：

1. 安装版选择“自动检测安装版或注册表配置”。
2. 便携版选择“便携版 MobaXterm.ini”，再点击“浏览...”或“自动查找”。
3. 点击“开始恢复”。
4. 结果默认脱敏；需要查看时勾选“显示明文密码”。
5. 可以复制当前显示内容或清空结果。

程序不会自动把恢复结果写入文件。

## 从源码构建

需要 .NET SDK：

```powershell
git clone https://github.com/MushGrowth/MobaXtermPasswordRecovery.git
cd MobaXtermPasswordRecovery
dotnet publish .\MobaXtermPasswordRecovery.csproj -c Release
```

默认生成单文件程序：

```text
bin\Release\publish\MobaXtermPasswordRecovery.exe
```

## 安全建议

- 优先自行审查源码并本地编译。
- 不要提交 `MobaXterm.ini`、日志、终端输出或任何凭据数据。
- 恢复密码后建议立即轮换密码，并改用 SSH 密钥等更安全的认证方式。
- 凭据恢复工具可能被安全软件标记，请根据源码、构建来源和使用环境自行判断。

## 致谢

- 原项目：[h0ny/MobaXtermDecryptor](https://github.com/h0ny/MobaXtermDecryptor)
- 参考项目：[HyperSine/how-does-MobaXterm-encrypt-password](https://github.com/HyperSine/how-does-MobaXterm-encrypt-password)

## License

MIT License。分发本项目或其修改版本时，必须保留仓库中的原作者版权声明和许可文本。
