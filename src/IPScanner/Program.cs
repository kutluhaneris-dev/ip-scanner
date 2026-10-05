using IPScanner.Core;
using IPScanner.UI;

namespace IPScanner;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Beklenmeyen bir hata programı kapatmasın; ayrıntısını kullanıcıya göster ve kaydet.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportError(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { LogError(e.Exception); e.SetObserved(); };

        Application.Run(new MainForm());
    }

    private static string LogPath => Path.Combine(Path.GetDirectoryName(AppSettings.FilePath)!, "hata.log");

    private static void ReportError(Exception? ex)
    {
        if (ex == null) return;
        LogError(ex);
        MessageBox.Show(
            "Beklenmeyen bir hata oluştu, program çalışmaya devam ediyor.\n\n" +
            $"{ex.GetType().Name}: {ex.Message}\n\n" +
            $"Ayrıntılar şu dosyaya kaydedildi:\n{LogPath}",
            "IP Tarayıcı – Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Günlük yazılamazsa yapacak bir şey yok.
        }
    }
}
