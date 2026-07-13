# MobaXtermPasswordRecovery

一个用于恢复当前 Windows 用户所保存的 MobaXterm 凭据的开源工具，支持安装版注册表配置和便携版 `MobaXterm.ini`。

本项目基于 [h0ny/MobaXtermDecryptor](https://github.com/h0ny/MobaXtermDecryptor) 修改并继续维护。原项目版权归原作者所有，本项目遵循 MIT License。

## 改进内容

- 兼容缺少 `[Credentials]` 或 `[Passwords]` 节的配置文件。
- 允许 MobaXterm 的 `[Macros]` 等配置节包含重复键。
- 展开反射调用异常，输出实际的异常类型和原因。
- 使用新的项目及可执行文件名称 `MobaXtermPasswordRecovery`。

## 使用限制

本工具仅用于恢复你本人拥有或已获明确授权管理的凭据。便携版的 Master Password 数据可能受 Windows DPAPI 保护，因此通常需要在保存密码时使用的原电脑、原 Windows 用户环境中运行。

运行结果可能包含明文密码。请勿截图、上传、共享或将输出保存到公共位置。

## 构建

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

## 使用方法

安装版 MobaXterm：

```powershell
.\MobaXtermPasswordRecovery.exe --debug
```

便携版 MobaXterm：

```powershell
.\MobaXtermPasswordRecovery.exe mobaxterm --debug "D:\Path\To\MobaXterm.ini"
```

查看帮助：

```powershell
.\MobaXtermPasswordRecovery.exe --help
.\MobaXtermPasswordRecovery.exe mobaxterm --help
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
