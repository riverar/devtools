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
using System.Windows.Shapes;

namespace CoApp.Bootstrapper {
    /// <summary>
    /// Interaction logic for AreYouSure.xaml
    /// </summary>
    public partial class AreYouSure : Window {
        public AreYouSure() {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) {
            // cancel the request.
            DialogResult = false;
            Close();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) {
            // cancel the request.
            DialogResult = false;
            Close();
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
            Close();
        }
    }
}
