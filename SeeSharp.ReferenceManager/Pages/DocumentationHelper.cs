using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SeeSharp.Blazor;

public static class DocumentationHelper
{
    private static Dictionary<string, string> _loadedXmlDocumentation = new();

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
                        _loadedXmlDocumentation[name] = cleanSummary;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocHelper] Error loading XML: {ex.Message}");
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

        if (_loadedXmlDocumentation.TryGetValue(key, out var summary))
        {
            return summary;
        }

        return "";
    }
}