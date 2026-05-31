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
    /// Logica di interazione per Levels.xaml
    /// </summary>
    public partial class Levels : Window
    {
        private MainWindow _mainWindow;

        public bool started = false;
        public Levels(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeComponent();
            started = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            InputBlack.Text = "0";
            InputMidtones.Text = "0";
            InputGamma.Text = "0";
            _mainWindow.RawLab.InputBlack = 0;
            _mainWindow.RawLab.InputGamma = 0;
            _mainWindow.RawLab.InputWhite = 0;
            _mainWindow.RawLab.OutputBlack = 0;
            _mainWindow.RawLab.OutputWhite = 0;
            _mainWindow.RefreshPreview();
            this.Close();
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {


            if (started == true)
            {
                if (float.TryParse(InputBlack.Text, out float inputBlack) &&
                    float.TryParse(InputMidtones.Text, out float inputMidtones) &&
                    float.TryParse(OutputBlack.Text, out float outputBlack) &&
                    float.TryParse(OutputWhite.Text, out float outputWhite))
                {
                    if (inputBlack >= inputMidtones || outputBlack >= outputWhite)
                    {
                        return;
                    }
                }

                if (byte.TryParse(InputBlack.Text, out byte val0))
                {
                    _mainWindow.RawLab.InputBlack = val0;
                    _mainWindow.RefreshPreview();
                }
                if (byte.TryParse(InputMidtones.Text, out byte val1))
                {
                    _mainWindow.RawLab.InputWhite = val1;
                    _mainWindow.RefreshPreview();
                }

                if (float.TryParse(InputGamma.Text, out float val2))
                {
                    _mainWindow.RawLab.InputGamma = val2;
                    _mainWindow.RefreshPreview();
                }
            }
        }
    }
}
