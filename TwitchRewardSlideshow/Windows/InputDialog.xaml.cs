using System;
using System.Windows;

namespace TwitchRewardSlideshow.Windows {
    public partial class InputDialog : Window {
        public string result => AnswerTextBox.Text;

        public InputDialog(string text, bool isSecret, bool hideCancel, string defaultAnswer = "") {
            InitializeComponent();
            RequieredText.Content = text;
            AnswerTextBox.Text = defaultAnswer;
            if (isSecret) {
                AnswerTextBox.Visibility = Visibility.Visible;
                PassTextBox.Visibility = Visibility.Hidden;
            } else {
                PassTextBox.Visibility = Visibility.Visible;
                AnswerTextBox.Visibility = Visibility.Hidden;
            }
            if (hideCancel) CancelButton.Visibility = Visibility.Collapsed;
        }

        private void ClickAccept(object sender, RoutedEventArgs e) {
            DialogResult = true;
        }

        private void WindowContentRendered(object sender, EventArgs e) {
            AnswerTextBox.SelectAll();
            AnswerTextBox.Focus();
        }
    }
}