# EyesRest

Windows 系统托盘护眼提醒程序。

- 每累计工作 20 分钟提醒一次短休息。
- 短休息为 20 秒远眺：看 6 米以外，并慢慢眨眼。
- 每累计工作 2 小时提醒一次长休息。
- 长休息为 15 分钟离屏：上厕所、喝水、走动、看远处，不刷手机。
- 只有锁屏、熄屏或睡眠才算离开屏幕：离开超过 2 分钟视作已经短休息，离开达到长休息时长视作已经长休息。
- 不碰键鼠（看文档、看视频、开会）照常计入用眼时间，不会被当成休息。
- 休息前会先弹托盘提示（短休息提前 30 秒，长休息提前 60 秒），给你收尾的时间。
- 休息遮罩可选“稍后”（短休息 5 分钟、长休息 15 分钟），每次休息最多延后 2 次。
- 遮罩弹出后 1.5 秒内的 Esc 和点击不生效，避免打字时误跳过。
- 全屏应用、演示模式或全屏游戏时会推迟提醒，退出后再弹；最多推迟 30 分钟。
- 首次运行会自动开启当前用户的开机自启。
- 托盘右键可立即短休息、立即长休息、暂停 1 小时、继续计时、打开设置、切换开机自启、退出。
- 双击托盘图标可立即触发一次休息。
- 设置窗口可调整短休息、长休息、空闲重置和开机自启，保存后会重新开始本轮计时。

## 默认护眼模式

默认模式按“健康优先但可长期坚持”设计：

```json
{
  "MicroBreakWorkMinutes": 20,
  "MicroBreakSeconds": 20,
  "LongBreaksEnabled": true,
  "LongBreakWorkMinutes": 120,
  "LongBreakMinutes": 15,
  "IdleResetMinutes": 2
}
```

设置文件位于：

```text
%AppData%\EyesRest\settings.json
```

普通用户不需要修改设置文件。默认值就是推荐模式。
需要调整时，右键托盘图标打开“设置...”即可。

## 运行

```powershell
dotnet run -c Release
```

## 打包 exe

```powershell
dotnet publish -c Release --no-self-contained
```

发布后的程序位于：

```text
bin\Release\net10.0-windows\publish\EyesRest.exe
```

这个发布方式依赖本机已安装的 .NET 10 Windows Desktop 运行时。

## 给朋友使用

如果朋友没有安装 .NET 10 Windows Desktop 运行时，更适合打包成自包含版本：

```powershell
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true --self-contained true -o publish-final --source https://api.nuget.org/v3/index.json
```

发布后的朋友版位于：

```text
publish-final\EyesRest.exe
publish-final\EyesRest-final-win-x64.zip
```

这条命令首次运行需要能访问 NuGet 下载 Windows runtime pack。朋友运行这个 exe 不需要预先安装 .NET。
