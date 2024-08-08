using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace RunningLog
{
    public partial class SlideMessage : UserControl
    {
        public SlideMessage()
        {
            InitializeComponent();
        }

        public void ShowMessage(string message, MessageType type)
        {
            MessageText.Text = message;
            switch (type)
            {
                case MessageType.Success:
                    MessageBorder.Background = new SolidColorBrush(Colors.LightGreen);
                    break;
                case MessageType.Warning:
                    MessageBorder.Background = new SolidColorBrush(Colors.Yellow);
                    break;
                case MessageType.Error:
                    MessageBorder.Background = new SolidColorBrush(Colors.LightPink);
                    break;
            }

            var animation = new DoubleAnimation
            {
                From = -40,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            MessageBorder.BeginAnimation(Canvas.TopProperty, animation);

            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                var hideAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = -40,
                    Duration = TimeSpan.FromSeconds(0.5)
                };
                MessageBorder.BeginAnimation(Canvas.TopProperty, hideAnimation);
            };
            timer.Start();
        }
    }

    public enum MessageType
    {
        Success,
        Warning,
        Error
    }
}