using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EyesRest;

/// <summary>
/// 判断用户是否真的离开了屏幕：只有锁屏或显示器熄灭才算。
/// 不碰键鼠不等于眼睛在休息——看文档、看视频、开会时同样盯着屏幕。
/// </summary>
internal sealed class PresenceMonitor : NativeWindow, IDisposable
{
    private const int WmPowerBroadcast = 0x0218;
    private const int PbtPowerSettingChange = 0x8013;
    private const int DisplayStateOff = 0;

    private static readonly Guid ConsoleDisplayState = new("6FE69556-704A-47A0-8F24-C28D936FDA47");

    private readonly IntPtr _powerNotification;
    private DateTimeOffset? _lockedSince;
    private DateTimeOffset? _displayOffSince;
    private bool _disposed;

    public PresenceMonitor()
    {
        CreateHandle(new CreateParams());
        var guid = ConsoleDisplayState;
        _powerNotification = RegisterPowerSettingNotification(Handle, ref guid, 0);
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>用户离开屏幕多久了；在屏幕前时为 null。</summary>
    public TimeSpan? AwayDuration
    {
        get
        {
            var since = _lockedSince is { } locked && (_displayOffSince is not { } off || locked < off)
                ? locked
                : _displayOffSince;

            return since is { } s ? DateTimeOffset.Now - s : null;
        }
    }

    /// <summary>全屏应用、D3D 全屏游戏或演示模式正在前台，此时弹遮罩会打断演示或会议。</summary>
    public static bool IsFullscreenBusy
    {
        get
        {
            if (SHQueryUserNotificationState(out var state) != 0)
            {
                return false;
            }

            return state is QunsBusy or QunsRunningD3DFullScreen or QunsPresentationMode;
        }
    }

    private const int QunsBusy = 2;
    private const int QunsRunningD3DFullScreen = 3;
    private const int QunsPresentationMode = 4;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmPowerBroadcast && m.WParam.ToInt32() == PbtPowerSettingChange && m.LParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<PowerBroadcastSetting>(m.LParam);
            if (setting.PowerSetting == ConsoleDisplayState)
            {
                if (setting.Data == DisplayStateOff)
                {
                    _displayOffSince ??= DateTimeOffset.Now;
                }
                else
                {
                    _displayOffSince = null;
                }
            }
        }

        base.WndProc(ref m);
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _lockedSince ??= DateTimeOffset.Now;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                _lockedSince = null;
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        if (_powerNotification != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_powerNotification);
        }

        DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerBroadcastSetting
    {
        public Guid PowerSetting;
        public uint DataLength;
        public int Data;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);
}
