using Avalonia.Data.Converters;
using SteamUEDeployTool.Core.Models.Enums;

namespace SteamUEDeployTool.Desktop.Converters;

public static class StageToVisibilityConverter
{
    public static readonly IValueConverter IsBuilding = new FuncValueConverter<PushStage, bool>(
        stage => stage == PushStage.Building);

    public static readonly IValueConverter IsDeploying = new FuncValueConverter<PushStage, bool>(
        stage => stage == PushStage.Deploying);

    public static readonly IValueConverter IsCompleted = new FuncValueConverter<PushStage, bool>(
        stage => stage == PushStage.Completed);

    public static readonly IValueConverter IsFailed = new FuncValueConverter<PushStage, bool>(
        stage => stage == PushStage.Failed);

    public static readonly IValueConverter IsRunning = new FuncValueConverter<PushStage, bool>(
        stage => stage is PushStage.Validating or PushStage.Building or PushStage.Deploying);
}
