using StairFinishing.ViewModels;

namespace StairFinishing.Views
{
    public sealed partial class StairFinishingView
    {
        public StairFinishingView(StairFinishingViewModel viewModel)
        {
            DataContext = viewModel;
            InitializeComponent();
        }
    }
}