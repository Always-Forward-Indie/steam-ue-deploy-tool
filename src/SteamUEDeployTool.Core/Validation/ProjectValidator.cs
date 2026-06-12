using SteamUEDeployTool.Core.Models;

namespace SteamUEDeployTool.Core.Validation;

public static class ProjectValidator
{
    public static ValidationResult Validate(ProjectInfo project, BuildProfile profile)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(project.UProjectPath)
            || !File.Exists(project.UProjectPath))
            errors.Add($"UProject file not found at '{project.UProjectPath}'.");

        if (string.IsNullOrWhiteSpace(project.Engine.Path)
            || !Directory.Exists(project.Engine.Path))
            errors.Add($"Engine directory not found at '{project.Engine.Path}'.");

        var runUatPath = GetRunUatPath(project.Engine.Path);
        if (!File.Exists(runUatPath))
            errors.Add($"RunUAT not found at '{runUatPath}'. Verify the engine installation.");

        if (!Enum.IsDefined(profile.Platform))
            errors.Add($"Invalid platform: {profile.Platform}");

        if (!Enum.IsDefined(profile.BuildConfiguration))
            errors.Add($"Invalid build configuration: {profile.BuildConfiguration}");

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    public static ValidationResult ValidateUProjectFile(string uprojectPath)
    {
        if (string.IsNullOrWhiteSpace(uprojectPath))
            return ValidationResult.Failure(["UProject path is empty."]);

        if (!File.Exists(uprojectPath))
            return ValidationResult.Failure([$"UProject file not found: '{uprojectPath}'."]);

        if (!uprojectPath.EndsWith(".uproject", StringComparison.OrdinalIgnoreCase))
            return ValidationResult.Failure([$"File is not a .uproject: '{uprojectPath}'."]);

        return ValidationResult.Success();
    }

    public static string GetRunUatPath(string enginePath)
    {
        var batchFilesDir = Path.Combine(enginePath, "Engine", "Build", "BatchFiles");

        if (OperatingSystem.IsWindows())
            return Path.Combine(batchFilesDir, "RunUAT.bat");

        if (OperatingSystem.IsMacOS())
            return Path.Combine(batchFilesDir, "RunUAT.sh");

        return Path.Combine(batchFilesDir, "RunUAT.sh");
    }
}

public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    public static ValidationResult Success() => new(true, Array.Empty<string>());
    public static ValidationResult Failure(IEnumerable<string> errors) => new(false, errors.ToList());
    public static ValidationResult Failure(string error) => new(false, [error]);
}
