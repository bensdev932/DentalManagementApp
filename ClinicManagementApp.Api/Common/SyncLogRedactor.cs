using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ClinicManagementApp.Api.Common;

public static partial class SyncLogRedactor
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "firstName",
        "lastName",
        "fullName",
        "patientName",
        "name",
        "title",
        "phone",
        "patientPhone",
        "email",
        "notes",
        "consultationNotes",
        "password",
        "token",
        "accessToken",
        "refreshToken"
    };

    private static readonly Regex DigitRunRegex = new(@"(?:\d[\s-]*){6,}\d", RegexOptions.Compiled);

    public static string MaskFreeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        return DigitRunRegex.Replace(text, "***");
    }

    public static string RedactAndTruncate(string? body, int max = 2048)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        string result;
        try
        {
            var node = JsonNode.Parse(body.Trim());
            if (node is JsonObject or JsonArray)
            {
                RedactNode(node);
                result = node.ToJsonString(new JsonSerializerOptions
                {
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    WriteIndented = false
                });
            }
            else
            {
                result = MaskFreeText(body);
            }
        }
        catch (JsonException)
        {
            result = MaskFreeText(body);
        }

        if (result.Length > max)
        {
            int excess = result.Length - max;
            return result[..max] + $"…[+{excess} chars]";
        }

        return result;
    }

    private static void RedactNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var kvp in obj.ToList())
            {
                if (SensitiveKeys.Contains(kvp.Key))
                {
                    obj[kvp.Key] = "***";
                }
                else if (kvp.Value is JsonObject or JsonArray)
                {
                    RedactNode(kvp.Value);
                }
                else if (kvp.Value is JsonValue val && val.TryGetValue<string>(out var strVal) && !string.IsNullOrEmpty(strVal))
                {
                    var trimmed = strVal.Trim();
                    if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
                        (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
                    {
                        try
                        {
                            var innerNode = JsonNode.Parse(trimmed);
                            if (innerNode is JsonObject or JsonArray)
                            {
                                RedactNode(innerNode);
                                obj[kvp.Key] = innerNode.ToJsonString(new JsonSerializerOptions
                                {
                                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                                    WriteIndented = false
                                });
                            }
                        }
                        catch (JsonException)
                        {
                            // Not JSON, leave as is
                        }
                    }
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    RedactNode(item);
                }
            }
        }
    }
}

