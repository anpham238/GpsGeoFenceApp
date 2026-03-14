using CommunityToolkit.Mvvm.Input;
using GpsGeoFence.Models;

namespace GpsGeoFence.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}