using Xunit;
using SteamUEDeployTool.Infrastructure.Vdf;

namespace SteamUEDeployTool.Infrastructure.Tests.Vdf;

public class VdfSerializerTests
{
    [Fact]
    public void Serialize_SimpleKeyValue_CorrectFormat()
    {
        var input = new Dictionary<string, object>
        {
            ["TestRoot"] = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        var result = VdfSerializer.Serialize(input);

        Assert.Contains("\"TestRoot\"", result);
        Assert.Contains("\"key\"", result);
        Assert.Contains("\"value\"", result);
    }

    [Fact]
    public void Serialize_NestedObjects_Indented()
    {
        var input = new Dictionary<string, object>
        {
            ["Root"] = new Dictionary<string, object>
            {
                ["Child"] = new Dictionary<string, object>
                {
                    ["Leaf"] = "data"
                }
            }
        };

        var result = VdfSerializer.Serialize(input);

        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var rootIndex = Array.FindIndex(lines, l => l.Contains("\"Root\""));
        var childIndex = Array.FindIndex(lines, l => l.Contains("\"Child\""));
        var leafIndex = Array.FindIndex(lines, l => l.Contains("\"Leaf\""));

        Assert.True(rootIndex < childIndex, "Root should appear before Child");
        Assert.True(childIndex < leafIndex, "Child should appear before Leaf");
    }

    [Fact]
    public void Serialize_BooleanValue_WritesAsString()
    {
        var input = new Dictionary<string, object>
        {
            ["Root"] = new Dictionary<string, object>
            {
                ["enabled"] = true
            }
        };

        var result = VdfSerializer.Serialize(input);

        Assert.Contains("\"1\"", result);
    }

    [Fact]
    public void Serialize_IntegerValue_WritesAsString()
    {
        var input = new Dictionary<string, object>
        {
            ["Root"] = new Dictionary<string, object>
            {
                ["count"] = 42
            }
        };

        var result = VdfSerializer.Serialize(input);

        Assert.Contains("\"42\"", result);
    }
}
