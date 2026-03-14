using GpsGeoFence.Models;
using GpsGeoFence.PageModels;

namespace GpsGeoFence.Pages
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageModel model)
        {
            InitializeComponent();
            BindingContext = model;
        }
    }
}