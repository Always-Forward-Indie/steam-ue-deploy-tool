namespace SteamUEDeployTool.Core.Models.Enums;

public enum PushStage
{
    Idle,
    Validating,
    Building,
    Deploying,
    Completed,
    Failed
}
