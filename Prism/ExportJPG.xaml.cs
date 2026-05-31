using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Prism
{
    /// <summary>
    /// Logica di interazione per ExportJPG.xaml
    /// </summary>
    public partial class ExportJPG : Window
    {
        public ExportJPG()
        {
            InitializeComponent();
        }

        private void onExportExit(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void onExportJPG(object sender, RoutedEventArgs e)
        {
            if(txtExportFolder.Text == "")
            {
                MessageBox.Show("Please select an export folder.");
                return;
            }
        }

        private void onOpenSaveDialog(object sender, RoutedEventArgs e)
        {



        }
    }
}
