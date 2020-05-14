using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FluentFTP;
using Recaptures.Properties;
using Serilog;

namespace Recaptures
{
    public class Recaptures
    {
        static System.Timers.Timer _shoot;
        private ILogger _log;

        public Recaptures()
        {
            if (Settings.Default.Logging)
            {
                _log = new LoggerConfiguration()
                    .WriteTo.File(Settings.Default.LogFile, rollingInterval: RollingInterval.Day)
                    .CreateLogger();
            }
            else
            {
                _log = new LoggerConfiguration().CreateLogger();
            }
        }

        public void Start()
        {

            _log = new LoggerConfiguration()
                .WriteTo.File(@"c:\temp\log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            _log.Information("Recaptures Start");

            _shoot = new System.Timers.Timer();
            _shoot.AutoReset = false;
            _shoot.Elapsed += new System.Timers.ElapsedEventHandler(t_Elapsed);
            _shoot.Interval = 60000;
            _shoot.Start();

            RunRecaptures();
        }

        public void Stop()
        {
            try
            {
                _log.Information("Recaptures Stop");

                _shoot.Stop();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void t_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            RunRecaptures();
            _shoot.Start();
        }

        public void RunRecaptures()
        {
            try
            {
                var filename = $"Recaptures-{DateTime.Now.Ticks}.jpg";
                var localTemp = Path.Combine(Path.GetTempPath(), filename);

                _log.Information($"Run {DateTime.Now.ToString("o")}");

                TakeFullScreenshot(localTemp);
                UploadScreenshot(localTemp, filename);
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
            }
        }

        private static void UploadScreenshot(string localTemp, string filename)
        {
            FtpClient client = new FtpClient(Settings.Default.Host);
            client.Credentials = new NetworkCredential(Settings.Default.UserName, Settings.Default.UserPassword);
            client.EncryptionMode = FtpEncryptionMode.Explicit;
            client.ValidateAnyCertificate = true;
            client.Connect();
            client.RetryAttempts = 3;
            client.UploadFile(localTemp, filename, FtpRemoteExists.Overwrite, false, FtpVerify.Retry);
            client.Disconnect();
        }

        private void TakeFullScreenshot(String fullpath)
        {
            SetDpiAwareness();

            Rectangle bounds = Screen.PrimaryScreen.Bounds;

            using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                System.Drawing.Imaging.Encoder myEncoder = System.Drawing.Imaging.Encoder.Quality;
                EncoderParameters myEncoderParameters = new EncoderParameters(1);
                EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, Settings.Default.JpegQuality);
                myEncoderParameters.Param[0] = myEncoderParameter;
                bitmap.Save(fullpath, jpgEncoder, myEncoderParameters);
            }
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                {
                    return codec;
                }
            }
            return null;
        }

        private enum ProcessDPIAwareness
        {
            ProcessDPIUnaware = 0,
            ProcessSystemDPIAware = 1,
            ProcessPerMonitorDPIAware = 2
        }

        [DllImport("shcore.dll")]
        private static extern int SetProcessDpiAwareness(ProcessDPIAwareness value);

        private void SetDpiAwareness()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    SetProcessDpiAwareness(ProcessDPIAwareness.ProcessPerMonitorDPIAware);
                }
            }
            catch (EntryPointNotFoundException ex)
            {
                _log.Error(ex.ToString());
            }
        }
    }
}
