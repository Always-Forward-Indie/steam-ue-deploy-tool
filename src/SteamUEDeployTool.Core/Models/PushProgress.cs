using CommunityToolkit.Mvvm.ComponentModel;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Core.Models;

public partial class PushProgress : ObservableObject
{
    [ObservableProperty]
    private PushStage _stage = PushStage.Idle;

    [ObservableProperty]
    private double _percent;

    [ObservableProperty]
    private string? _currentAction;
}
