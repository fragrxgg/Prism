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
    /// Logica di interazione per HistogramWindow.xaml
    /// </summary>
    public partial class HistogramWindow : Window
    {
        public HistogramWindow()
        {
            InitializeComponent();
        }

        public void DrawHistogram(int[][] hist)
        {
            HistoCanvas.Children.Clear();
        }
        }
}
