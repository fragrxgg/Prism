using Microsoft.Win32;
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


        public int quality { get; set; }
        public int dpi { get; set; }
        public string exportFolder { get; set; } = string.Empty;

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

            this.Close();
            //int quality, dpi;

            quality = txtQuality.Text != "" ? int.Parse(txtQuality.Text) : 100;
            dpi = txtDPI.Text != "" ? int.Parse(txtDPI.Text) : 96;
            exportFolder = txtExportFolder.Text;

        }

        private void onOpenSaveDialog(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JPEG file|*.jpg;*.jpeg;*.jpe;*.jfif|All files|*.*";

            if (saveFileDialog.ShowDialog() == true)
            {
                txtExportFolder.Text = saveFileDialog.FileName;
            }
        }
    }
}
