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

namespace Mini_GO_compiler.View.UserControls
{
    /// <summary>
    /// Lógica de interacción para InputFileMessageBox.xaml
    /// </summary>
    public partial class InputFileMessageBox : Window
    {
        public string Answer => txtInput.Text;
        public InputFileMessageBox(string prompt, string inputBoxName, string defaultAnswer = "")
        {
            InitializeComponent();
            InputMessage.Text = prompt;
            Title = inputBoxName;
            txtInput.Text = defaultAnswer;
            
            // Auto-focus textbox upon loading
            Loaded += (s, e) => txtInput.Focus();
        }
        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true; // Closes dialog and flags success
        }
    }
}
