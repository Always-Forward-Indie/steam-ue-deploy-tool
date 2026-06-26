namespace SteamUEDeployTool.Core.Models.Enums;

public enum PushStage
{
    Idle,
    Validating,
    Building,
    Bundling,
    Deploying,
    Completed,
    Failed
}
