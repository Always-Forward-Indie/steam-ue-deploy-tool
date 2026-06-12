using Xunit;
using SteamUEDeployTool.Core.Models;
using SteamUEDeployTool.Core.Models.Enums;
using SteamUEDeployTool.Core.Validation;

namespace SteamUEDeployTool.Core.Tests.Validation;

public class ProjectValidatorTests
{
    [Fact]
    public void ValidateUProjectFile_NullPath_ReturnsInvalid()
    {
        var result = ProjectValidator.ValidateUProjectFile(null!);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty"));
    }

    [Fact]
    public void ValidateUProjectFile_EmptyPath_ReturnsInvalid()
    {
        var result = ProjectValidator.ValidateUProjectFile("");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateUProjectFile_WhitespacePath_ReturnsInvalid()
    {
        var result = ProjectValidator.ValidateUProjectFile("   ");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateUProjectFile_NonExistentFile_ReturnsInvalid()
    {
        var result = ProjectValidator.ValidateUProjectFile("C:\\nonexistent\\file.uproject");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateUProjectFile_WrongExtension_ReturnsInvalid()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var result = ProjectValidator.ValidateUProjectFile(tempFile);
            Assert.False(result.IsValid);
            Assert.Contains(result.Errors, e => e.Contains("not a .uproject"));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetRunUatPath_OnWindows_ReturnsBatFile()
    {
        var result = ProjectValidator.GetRunUatPath("C:\\Engine");
        Assert.EndsWith(".bat", result);
    }

    [Fact]
    public void GetRunUatPath_ContainsBatchFilesFolder()
    {
        var result = ProjectValidator.GetRunUatPath("/opt/engine");
        Assert.Contains("BatchFiles", result);
    }
}
