using System.Text;

namespace SteamUEDeployTool.Infrastructure.Vdf;

public static class VdfSerializer
{
    public static string Serialize(Dictionary<string, object> root)
    {
        var sb = new StringBuilder();
        WriteObject(sb, root, 0);
        return sb.ToString();
    }

    private static void WriteObject(StringBuilder sb, Dictionary<string, object> obj, int indent)
    {
        var pad = new string('\t', indent);

        foreach (var (key, value) in obj)
        {
            sb.Append(pad);
            WriteKeyValue(sb, key, value, indent);
        }
    }

    private static void WriteKeyValue(StringBuilder sb, string key, object value, int indent)
    {
        sb.Append('"').Append(Escape(key)).Append('"');

        switch (value)
        {
            case string str:
                sb.Append("\t\t\"").Append(Escape(str)).Append('"').AppendLine();
                break;

            case Dictionary<string, object> nested:
                sb.AppendLine();
                WriteIndentedLine(sb, "{", indent);
                WriteObject(sb, nested, indent + 1);
                WriteIndentedLine(sb, "}", indent);
                break;

            case List<Dictionary<string, object>> list:
                sb.AppendLine();
                WriteIndentedLine(sb, "{", indent);
                foreach (var item in list)
                {
                    WriteObject(sb, item, indent + 1);
                }
                WriteIndentedLine(sb, "}", indent);
                break;

            case bool b:
                sb.Append("\t\t\"").Append(b ? "1" : "0").Append('"').AppendLine();
                break;

            case int i:
                sb.Append("\t\t\"").Append(i).Append('"').AppendLine();
                break;

            case uint ui:
                sb.Append("\t\t\"").Append(ui).Append('"').AppendLine();
                break;

            case long l:
                sb.Append("\t\t\"").Append(l).Append('"').AppendLine();
                break;

            default:
                sb.Append("\t\t\"").Append(Escape(value.ToString() ?? "")).Append('"').AppendLine();
                break;
        }
    }

    private static void WriteIndentedLine(StringBuilder sb, string text, int indent)
    {
        sb.Append(new string('\t', indent)).Append(text).AppendLine();
    }

    private static string Escape(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
    }
}
