using System;
using System.Windows;
using TwitchRewardSlideshow.Utilities;
using XamlAnimatedGif;

namespace TwitchRewardSlideshow.Windows {
    public partial class ImageInputDialog : Window {
        public string result => AnswerTextBox.Text;

        public ImageInputDialog(string text, string imageName, bool isSecret, bool hideCancel,
            string defaultAnswer = "") {
            InitializeComponent();
            RequieredText.Content = text;
            AnswerTextBox.Text = defaultAnswer;

            Uri imageUri = new("../Resources/" + imageName, UriKind.Relative);

            if (imageName.EndsWith(".gif")) {
                AnimationBehavior.SetSourceUri(HelpImage, imageUri);
            } else HelpImage.Source = ImageUtilities.BitmapFromUri(imageUri);

            if (isSecret) {
                AnswerTextBox.Visibility = Visibility.Hidden;
                PassTextBox.Visibility = Visibility.Visible;
            } else {
                PassTextBox.Visibility = Visibility.Hidden;
                AnswerTextBox.Visibility = Visibility.Visible;
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