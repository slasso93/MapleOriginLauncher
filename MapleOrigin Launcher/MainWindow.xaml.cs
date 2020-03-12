using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MapleOrigin_Launcher
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
            launcher = new Launcher(progressBar, play, update);
        }

        private void PlayGame_Click(object sender, RoutedEventArgs e)
        {
            launcher.PlayGame();
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            launcher.UpdateGame();
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

        }

    }
}
