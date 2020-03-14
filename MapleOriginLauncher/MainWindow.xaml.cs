using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MapleOriginLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Launcher launcher;
        private bool isUpdating;
        private bool isChecking;

        public MainWindow()
        {
            InitializeComponent();
            launcher = new Launcher(progressBar, button, label);
            launcher.CheckForUpdates();
            button.IsEnabled = false;
            isChecking = true;
            isUpdating = false;
            Task.Factory.StartNew(() =>
            {
                labelUpdate();
            });
        }

        private void labelUpdate()
        {
            int i = 0;
            while (checking())
            {
                Thread.Sleep(100);


                Dispatcher.Invoke(() =>
                {
                    label.Content = "Checking for Updates " + new string('.', i % 10);
                });
                i++;
            }
            Dispatcher.Invoke(() =>
            {
                if (launcher.LauncherNeedsUpdate())
                {
                    label.Content = "Launcher is outdated! Please use the Restart button to get the latest MapleOriginLauncher";
                    button.Content = "Restart";
                }

                if (button.Content.Equals("Play Game"))
                {
                    label.Content = "Ready to play.";
                }
                else if (button.Content.Equals("Update Game"))
                {
                    label.Content = "Updates pending!";
                }
                progressBar.Value = 100;
            });
        }

        private bool checking()
        {
            Dispatcher.Invoke(() => {
                isChecking = !button.IsEnabled; 
            });
            return isChecking;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            button.IsEnabled = false;
            if (launcher.LauncherNeedsUpdate())
            {
                launcher.RunLauncherUpdater();
            }
            else if (button.Content.Equals("Play Game"))
            {
                launcher.PlayGame();
            }
            else if(button.Content.Equals("Update Game"))
            {
                launcher.UpdateGame();
                Task.Factory.StartNew(() =>
                {
                    waitForComplete();
                });
            }
        }

        private void waitForComplete()
        {
            while (updating())
            {
                Thread.Sleep(2000);
            }
            Dispatcher.Invoke(() =>
            {

                label.Content = "Ready to play.";
            });
        }

        private bool updating()
        {
            Dispatcher.Invoke(() => {
                isUpdating = button.Content.Equals("Update Game");
            });
            return isUpdating;
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

    }
}
