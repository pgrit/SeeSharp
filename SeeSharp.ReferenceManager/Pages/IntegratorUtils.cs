using System.Reflection;

namespace SeeSharp.ReferenceManager.Pages;

public class ParameterGroup
{
    public string Title { get; set; } = "";
    public List<PropertyInfo> Properties { get; set; } = new();
    public List<FieldInfo> Fields { get; set; } = new();
    public bool HasParameters => Properties.Any() || Fields.Any();
}

public static class IntegratorUtils
{
    public static List<ParameterGroup> GetParameterGroups(Integrator integrator)
    {
        var groups = new List<ParameterGroup>();
        var currentType = integrator.GetType();

        var allProps = GetFilteredProps(currentType);
        var allFields = GetFilteredFields(currentType);

        while (currentType != null && currentType != typeof(object))
        {
            bool IsCurrentDeclared(MemberInfo m)
            {
                var d = m.DeclaringType;
                var cur = currentType;
                if (d != null && d.IsGenericType && !d.IsGenericTypeDefinition)
                    d = d.GetGenericTypeDefinition();
                if (cur != null && cur.IsGenericType && !cur.IsGenericTypeDefinition)
                    cur = cur.GetGenericTypeDefinition();
                return d == cur;
            }

            string title = FormatClassName(currentType);

            bool isGlobalSettings = (currentType == typeof(Integrator));
            if (isGlobalSettings)
                title = "Global Settings";

            var group = new ParameterGroup
            {
                Title = title,
                Properties = allProps
                    .Where(p => IsCurrentDeclared(p))
                    .Where(p => !isGlobalSettings || (p.Name != "MaxDepth" && p.Name != "MinDepth"))
                    .ToList(),
                Fields = allFields.Where(f => IsCurrentDeclared(f)).ToList(),
            };

            if (group.HasParameters)
                groups.Add(group);

            currentType = currentType.BaseType;
        }

        return groups;
    }

    public static string FormatClassName(Type t)
    {
        string name = t.Name;
        if (name.Contains('`'))
            name = name.Substring(0, name.IndexOf('`'));
        return System.Text.RegularExpressions.Regex.Replace(name, "(\\B[A-Z])", " $1");
    }

    public static bool IsVisible(MemberInfo m)
    {
        if (m is PropertyInfo p && (!p.CanRead || !p.CanWrite))
            return false;
        if (m is FieldInfo f && (f.IsLiteral || f.IsInitOnly))
            return false;

        Type t = (m is PropertyInfo pi) ? pi.PropertyType : ((FieldInfo)m).FieldType;
        Type underlyingType = Nullable.GetUnderlyingType(t) ?? t;

        return underlyingType.IsPrimitive;
    }

    public static IEnumerable<PropertyInfo> GetFilteredProps(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(IsVisible);

    public static IEnumerable<FieldInfo> GetFilteredFields(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(IsVisible);

    public static string GetDescription(MemberInfo member)
    {
        return DocumentationReader.GetSummary(member) ?? "";
    }
}
