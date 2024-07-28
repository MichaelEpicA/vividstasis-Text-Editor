using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace vividstasis_Text_Editor
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(true);
            Application.ThreadException += ExceptionHandler;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            try
            {
                Application.Run(new Form1());
            } catch(Exception ex)
            {
                File.WriteAllText(Path.Combine(GetExecutableDirectory(), "crash.txt"), (ex.ToString() + "\n" + ex.Message + "\n" + ex.StackTrace));
                MessageBox.Show("A generic UI thread error occured and was not caught. Check crash.txt for more information. If you need help, report it to the official github.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
        }
        public static string GetExecutableDirectory()
        {
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            File.WriteAllText(Path.Combine(GetExecutableDirectory(), "crash2.txt"), (ex.ToString() + "\n" + ex.Message + "\n" + ex.StackTrace));
            MessageBox.Show("A generic error occured and was not caught. Check crash2.txt for more information. If you need help, report it to the official github.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void ExceptionHandler(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Exception ex = (Exception)e.Exception;
            File.WriteAllText(Path.Combine(GetExecutableDirectory(), "crash3.txt"), (ex.ToString() + "\n" + ex.Message + "\n" + ex.StackTrace));
            MessageBox.Show("A generic UI thread error occured and was not caught. Check crash.txt for more information. If you need help, report it to the official github.", "vivid/stasis Text Editor", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}
