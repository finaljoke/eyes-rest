using System.Threading;

namespace EyesRest;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, name: @"Local\EyesRest", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("EyesRest 已在运行。", "EyesRest", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(defaultValue: false);
        Application.Run(new EyeRestApplicationContext());
    }
}
