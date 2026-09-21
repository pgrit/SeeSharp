using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using SeeSharp.Integrators;

namespace SeeSharp.Blazor;

public class ModuleNode
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<object> TargetInstances { get; set; } = new();
    public Type NodeType { get; set; } = default!;
    public bool IsSharedClass { get; set; }
    public bool IsInstanceBranch { get; set; }

    public List<PropertyInfo> Properties { get; set; } = new();
    public List<FieldInfo> Fields { get; set; } = new();
    public List<ModuleNode> Children { get; set; } = new();
    public ModuleNode? ParentNode { get; set; }
    public MemberInfo? MemberInParent { get; set; }
}

public partial class IntegratorTree : ComponentBase
{
    [Parameter] public List<Integrator> Integrators { get; set; } = new();
    [Parameter] public Dictionary<Integrator, string> Names { get; set; } = new();
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }
    [Parameter] public EventCallback OnDataChanged { get; set; }

    public List<ModuleNode> Roots { get; set; } = new();
    public ModuleNode? SelectedNode { get; set; }

    protected override void OnParametersSet()
    {
        if (IsVisible && Integrators.Any())
        {
            BuildInheritanceTree();
        }
    }

    private void BuildInheritanceTree()
    {
        Roots.Clear();

        var root = new ModuleNode {
            Id = "Root_Integrator",
            Name = "Integrator base",
            NodeType = typeof(Integrator),
            IsSharedClass = true
        };

        var rootFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        root.Properties.AddRange(typeof(Integrator).GetProperties(rootFlags).Where(p => p.CanWrite));
        root.Fields.AddRange(typeof(Integrator).GetFields(rootFlags));

        foreach (var integrator in Integrators)
        {
            if (!root.TargetInstances.Contains(integrator))
                root.TargetInstances.Add(integrator);

            var chain = new List<Type>();
            var t = integrator.GetType();
            while (t != null && t != typeof(object)) {
                chain.Insert(0, t);
                t = t.BaseType;
            }

            ModuleNode currentNode = root;
            foreach (var type in chain)
            {
                if (type == typeof(Integrator)) continue;

                var existing = currentNode.Children.FirstOrDefault(c => c.NodeType == type && c.IsSharedClass);
                if (existing == null)
                {
                    existing = new ModuleNode {
                        Id = "Class_" + type.FullName,
                        Name = IntegratorUtils.FormatClassName(type),
                        NodeType = type,
                        IsSharedClass = true
                    };

                    var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                    existing.Properties.AddRange(type.GetProperties(flags).Where(p => p.CanWrite));
                    existing.Fields.AddRange(type.GetFields(flags));

                    currentNode.Children.Add(existing);
                }

                if (!existing.TargetInstances.Contains(integrator))
                    existing.TargetInstances.Add(integrator);

                currentNode = existing;
            }

            Names.TryGetValue(integrator, out var instanceName);
            currentNode.Children.Add(new ModuleNode {
                Id = "Instance_" + integrator.GetHashCode(),
                Name = "Instance: " + (instanceName ?? integrator.GetType().Name),
                TargetInstances = new List<object> { integrator },
                NodeType = integrator.GetType(),
                IsInstanceBranch = true
            });
        }
        Roots.Add(root);
    }

    public void SelectNode(ModuleNode node) => SelectedNode = node;

    public async Task Close() => await OnClose.InvokeAsync();

    public void HandleParamChange(MemberInfo member, ModuleNode node, object v)
    {
        foreach (var target in node.TargetInstances)
        {
            if (member is PropertyInfo p) p.SetValue(target, v);
            else if (member is FieldInfo f) f.SetValue(target, v);

            var cur = node;
            var currentTarget = target;

            while (cur.ParentNode != null && cur.MemberInParent != null)
            {
                if (currentTarget.GetType().IsValueType)
                {
                    var parentTarget = cur.ParentNode.TargetInstances.FirstOrDefault();
                    if (parentTarget != null)
                    {
                        if (cur.MemberInParent is PropertyInfo pi) pi.SetValue(parentTarget, currentTarget);
                        else if (cur.MemberInParent is FieldInfo fi) fi.SetValue(parentTarget, currentTarget);

                        currentTarget = parentTarget;
                    }
                    else break;
                }
                else break;

                cur = cur.ParentNode;
            }
        }

        _ = OnDataChanged.InvokeAsync();
    }
}