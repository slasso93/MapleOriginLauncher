using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MapleOriginLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private Launcher launcher;

        public MainWindow()
        {
            InitializeComponent();
            launcher = new Launcher(progressBar, button, label);
            launcher.CheckForUpdates();
            button.IsEnabled = false;
            Task.Factory.StartNew(() =>
            {
                InvokeMethodExample();
            });
        }

        private void InvokeMethodExample()
        {
            int i = 0;
            while (updating())
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
                if (button.Content.Equals("Play Game"))
                {
                    label.Content = "Ready to play.";
                }
                else if (button.Content.Equals("Update Game"))
                {
                    label.Content = "Updated pending!";
                }
            });
        }

        private bool updating()
        {
            bool isUpdating = false;
            Dispatcher.Invoke(() => {
                isUpdating = !button.IsEnabled; 
            });
            return isUpdating;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            button.IsEnabled = false;
            if (button.Content.Equals("Play Game"))
            {
                launcher.PlayGame();
            }
            else if(button.Content.Equals("Update Game"))
            {
                launcher.UpdateGame();
            }
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

    }
}
