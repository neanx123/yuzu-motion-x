using System.Windows.Forms;

namespace YuzuMotionX;

static class Program
{
    [STAThread]
    static void Main()
    {
        // only one copy at a time: two copies would fight over the same ports
        using (var mutex = new Mutex(true, "Global\\YuzuMotionX_v1_NeanX", out bool created))
        {
            if (!created)
            {
                MessageBox.Show("Yuzu Motion X is already running.", "Yuzu Motion X v1.0",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            AppSettings settings = AppSettings.Load();
            AppSettings.Current = settings;
            RawInputHub.Start();                        // mouse + keyboard raw input, on its own thread

            var runners = new PlayerRunner[AppSettings.PlayerCount];
            var problems = new List<string>();
            for (int i = 0; i < runners.Length; i++)
            {
                runners[i] = new PlayerRunner(i, settings.Players[i]);
                runners[i].Start();
                if (settings.Players[i].On && !runners[i].StartServer(out string err))
                {
                    settings.Players[i].On = false;
                    problems.Add("Player " + (i + 1) + " (" + settings.Players[i].Host + ":" + settings.Players[i].Port + "): " + err);
                }
            }

            if (problems.Count > 0)
            {
                MessageBox.Show("These players could not start and were switched OFF:\n\n" + string.Join("\n", problems) +
                                "\n\nThe port is probably used by another program. Change the server:port in the player's tab.",
                                "Yuzu Motion X v1.0", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Application.Run(new MainWindow(settings, runners));

            foreach (var r in runners) r.Shutdown();
        }
    }
}
