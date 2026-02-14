using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SeeSharp.Common;

/// <summary>
/// Load xml documentation files and retrieve summary to show description
/// </summary>
public static class DocumentationReader
{
    private static Dictionary<string, string> loadedXmlDocumentation = new();

    public static void LoadXmlDocumentation(Assembly assembly)
    {
        var assemblyPath = assembly.Location;
        var xmlPath = Path.ChangeExtension(assemblyPath, ".xml");

        if (File.Exists(xmlPath))
        {
            try
            {
                var doc = XDocument.Load(xmlPath);
                foreach (var member in doc.Descendants("member"))
                {
                    var name = member.Attribute("name")?.Value;
                    var summary = member.Element("summary")?.Value.Trim();

                    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(summary))
                    {
                        string cleanSummary = Regex.Replace(summary, @"\s+", " ");
                        loadedXmlDocumentation[name] = cleanSummary;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error loading XML: {ex.Message}", Verbosity.Error);
            }
        }
    }

    public static string GetSummary(MemberInfo member)
    {
        if (member == null || member.DeclaringType == null) return "";

        string prefix = member is PropertyInfo ? "P:" : "F:";

        Type declaringType = member.DeclaringType;
        string typeName = declaringType.FullName;

        if (declaringType.IsGenericType)
        {
            int bracketIndex = typeName.IndexOf('[');
            if (bracketIndex > 0)
            {
                typeName = typeName.Substring(0, bracketIndex);
            }
        }

        string key = $"{prefix}{typeName}.{member.Name}";

        if (loadedXmlDocumentation.TryGetValue(key, out var summary))
        {
            return summary;
        }

        return "";
    }
}
