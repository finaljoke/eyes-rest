namespace EyesRest;

internal enum ReminderKind
{
    Micro,
    Long
}

internal sealed record BreakReminder(
    ReminderKind Kind,
    TimeSpan Duration,
    string Title,
    string Subtitle,
    string Guidance,
    string CompletionText)
{
    public static BreakReminder Create(ReminderKind kind, EyeCareSettings settings)
    {
        return kind switch
        {
            ReminderKind.Long => new BreakReminder(
                kind,
                settings.LongBreakDuration,
                $"离开屏幕 {settings.LongBreakMinutes} 分钟",
                "站起来走动，喝水，看看窗外或远处",
                "这次休息用于恢复眼睛、肩颈和专注力。不要刷手机。",
                "完成长休息"),
            _ => new BreakReminder(
                kind,
                settings.MicroBreakDuration,
                $"看远处 {settings.MicroBreakSeconds} 秒",
                "望向 6 米以外，慢慢眨眼 5 次",
                "放松眉头、肩颈和下颌，让眼睛从近距离聚焦里退出来。",
                "我已完成")
        };
    }
}
