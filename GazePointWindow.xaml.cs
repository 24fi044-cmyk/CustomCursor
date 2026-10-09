using System.Windows;

namespace CustomCursor
{
    public partial class GazePointWindow : Window
    {
        public GazePointWindow()
        {
            InitializeComponent();
        }

        public void SetPosition(
            double x,
            double y)
        {
            Left =
                x - Width / 2;

            Top =
                y - Height / 2;
        }
    }
}