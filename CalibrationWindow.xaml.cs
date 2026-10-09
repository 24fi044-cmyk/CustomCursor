using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace CustomCursor
{
    public partial class CalibrationWindow : Window
    {
        public event EventHandler? CancelRequested;

        public CalibrationWindow()
        {
            InitializeComponent();
        }

        public void SetTarget(int pointNumber, double normalizedX, double normalizedY)
        {
            double width = ActualWidth > 0 ? ActualWidth : SystemParameters.PrimaryScreenWidth;
            double height = ActualHeight > 0 ? ActualHeight : SystemParameters.PrimaryScreenHeight;
            double x = width * normalizedX;
            double y = height * normalizedY;

            Canvas.SetLeft(TargetHalo, x - TargetHalo.Width / 2);
            Canvas.SetTop(TargetHalo, y - TargetHalo.Height / 2);
            Canvas.SetLeft(TargetDot, x - TargetDot.Width / 2);
            Canvas.SetTop(TargetDot, y - TargetDot.Height / 2);
            InstructionText.Text =
                $"赤い点を見つめてください  ({pointNumber} / 9)\n" +
                "点が自動で移動するまで、顔を正面に保ってください";
        }

        public void SetStatus(string message)
        {
            InstructionText.Text = message;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CalibrationWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
