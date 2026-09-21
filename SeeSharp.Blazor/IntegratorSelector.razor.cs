using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SeeSharp.Integrators;
using SeeSharp.Common;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Reflection;

namespace SeeSharp.Blazor;

public partial class IntegratorSelector : ComponentBase
{
    [Inject] public IJSRuntime JS { get; set; } = default!;
    [Parameter] public Scene scene { get; set; } = default!;
    [Parameter] public EventCallback<Integrator> OnRunIntegrator { get; set; }
    [Parameter] public EventCallback<Integrator> OnDeleteIntegrator { get; set; }
    [Parameter] public EventCallback OnRunAllIntegrators { get; set; }

    public List<Integrator> addedIntegrators { get; private set; } = new();
    public Integrator globalIntegratorSettings { get; set; } = new PathTracer();
    public Dictionary<Integrator, string> Names { get; private set; } = new();

    public HashSet<string> DecoupledParameters { get; set; } = new();
    public Dictionary<Integrator, HashSet<string>> LocalDecoupledParameters { get; set; } = new();

    public bool isCascadeOpen = false;
    public string? activeCascadeModule = null;
    public Integrator? activeLocalMenuIntegrator = null;
    public string? activeLocalSub = null;
    public bool showTree = false;

    HashSet<Integrator> expandedItems = new();
    Integrator? draggedItem;
    Type[] integratorTypes = Array.Empty<Type>();
    string? selectedIntegrator;

    private Stack<(Integrator Ptr, int Index, string? Name, HashSet<string>? LocalOverrides, bool IsExpanded)> undoStack = new();
    private const string LocalStorageKey = "SeeSharp_LastConfig";

    protected override void OnInitialized()
    {
        var types = AppDomain
            .CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && typeof(Integrator).IsAssignableFrom(type)
                && !type.ContainsGenericParameters
                && !typeof(DebugVisualizer).IsAssignableFrom(type)
            );
        integratorTypes = types.Where(t => !types.Any(other => other.IsSubclassOf(t))).ToArray();

        if (integratorTypes.Length > 0)
        {
            selectedIntegrator = integratorTypes.First().FullName;
        }
        DocumentationReader.LoadXmlDocumentation(typeof(Integrator).Assembly);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
            if (firstRender)
            {
                await LoadConfigFromBrowser();
            }
    }

    void OnSelectionChanged(ChangeEventArgs e) => selectedIntegrator = e.Value?.ToString();

    public void SyncGlobalSettings()
    {
        var globalGroups = GetGlobalParameterGroups();

        foreach (var integrator in addedIntegrators)
        {
            var localDecoupled = LocalDecoupledParameters.GetValueOrDefault(integrator) ?? new();
            var targetHierarchy = IntegratorUtils.GetParameterGroups(integrator);

            foreach (var gGroup in globalGroups)
            {
                var tGroup = targetHierarchy.FirstOrDefault(tg => tg.Title == gGroup.Title);
                if (tGroup == null) continue;

                foreach (var p in gGroup.Properties)
                {
                    string path = $"{gGroup.Title}.{p.Name}";
                    if (!DecoupledParameters.Contains(path) && !localDecoupled.Contains(path))
                    {
                        var targetProp = tGroup.Properties.FirstOrDefault(tp => tp.Name == p.Name);
                        if (targetProp != null && targetProp.CanWrite)
                        {
                            object? val = GetBulkValue(gGroup.Title, p.Name);
                            if (val != null) targetProp.SetValue(integrator, val);
                        }
                    }
                }
                foreach (var f in gGroup.Fields)
                {
                    string path = $"{gGroup.Title}.{f.Name}";
                    if (!DecoupledParameters.Contains(path) && !localDecoupled.Contains(path))
                    {
                        var targetField = tGroup.Fields.FirstOrDefault(tf => tf.Name == f.Name);
                        if (targetField != null)
                        {
                            object? val = GetBulkValue(gGroup.Title, f.Name);
                            if (val != null) targetField.SetValue(integrator, val);
                        }
                    }
                }
            }
        }
    }

    // integrator management
    void AddIntegrator()
    {
        if (string.IsNullOrEmpty(selectedIntegrator)) return;
        var type = integratorTypes.FirstOrDefault(t => t.FullName == selectedIntegrator);
        if (type == null) return;

        var integrator = (Integrator)Activator.CreateInstance(type)!;
        Names[integrator] = GetNextAvailableName(FormatClassName(type));
        LocalDecoupledParameters[integrator] = new HashSet<string>();

        addedIntegrators.Add(integrator);
        CleanUpInvalidDecoupledParameters();
        expandedItems.Add(integrator);
        SyncGlobalSettings();
        _ = SaveConfigToBrowser();
    }

    void RunIntegrator(Integrator integrator)
    {
        _ = SaveConfigToBrowser();
        OnRunIntegrator.InvokeAsync(integrator);
    }
    void RunAllIntegrators()
    {
        _ = SaveConfigToBrowser();
        OnRunAllIntegrators.InvokeAsync();
    }

    void DeleteIntegrator(Integrator integrator)
    {
        undoStack.Push((integrator,
            addedIntegrators.IndexOf(integrator),
            Names.GetValueOrDefault(integrator),
            new HashSet<string>(LocalDecoupledParameters.GetValueOrDefault(integrator) ?? new()),
            expandedItems.Contains(integrator)));
        addedIntegrators.Remove(integrator);
        CleanUpInvalidDecoupledParameters();
        LocalDecoupledParameters.Remove(integrator);
        expandedItems.Remove(integrator);
        Names.Remove(integrator);
        OnDeleteIntegrator.InvokeAsync(integrator);
        _ = SaveConfigToBrowser();
    }

    void UndoDelete()
    {
        if (undoStack.TryPop(out var state))
        {
            if (state.Index >= 0 && state.Index <= addedIntegrators.Count)
            {
                addedIntegrators.Insert(state.Index, state.Ptr);
            }
            else
            {
                addedIntegrators.Add(state.Ptr);
            }
            LocalDecoupledParameters[state.Ptr] = state.LocalOverrides ?? new();
            if (state.IsExpanded) expandedItems.Add(state.Ptr);
            if (!string.IsNullOrEmpty(state.Name)) Names[state.Ptr] = state.Name;
        }
    }

    void DuplicateIntegrator(Integrator source)
    {
        var type = source.GetType();
        var newIntegrator = (Integrator)Activator.CreateInstance(type)!;

        foreach (var group in IntegratorUtils.GetParameterGroups(source))
        {
            foreach (var prop in group.Properties.Where(p => p.CanRead && p.CanWrite))
                prop.SetValue(newIntegrator, prop.GetValue(source));
            foreach (var field in group.Fields)
                field.SetValue(newIntegrator, field.GetValue(source));
        }

        Names[newIntegrator] = GetNextAvailableName(Names.GetValueOrDefault(source, FormatClassName(type)));
        LocalDecoupledParameters[newIntegrator] = new HashSet<string>(LocalDecoupledParameters.GetValueOrDefault(source) ?? new());
        addedIntegrators.Add(newIntegrator);
        expandedItems.Add(newIntegrator);
    }

    // UI menu
    public void ToggleCascade()
    {
        isCascadeOpen = !isCascadeOpen;
        activeCascadeModule = null;
    }
    public void CloseCascade()
    {
        isCascadeOpen = false;
        activeCascadeModule = null;
    }
    public void HandleMouseEnterModule(string title)
    {
        if (isCascadeOpen) activeCascadeModule = title;
    }
    public void RemoveUnlinkedParameter(string path)
    {
        DecoupledParameters.Remove(path);
        SyncGlobalSettings();
        _ = SaveConfigToBrowser();
    }

    public void ToggleLocalMenu(Integrator i)
    {
        activeLocalMenuIntegrator = activeLocalMenuIntegrator == i ? null : i;
        activeLocalSub = null;
    }
    public void CloseLocalMenu()
    {
        activeLocalMenuIntegrator = null;
        activeLocalSub = null;
    }
    public void AddLocalDecoupled(Integrator i, string path)
    {
        if (!LocalDecoupledParameters.ContainsKey(i)) LocalDecoupledParameters[i] = new();
        LocalDecoupledParameters[i].Add(path);
        CloseLocalMenu();
        _ = SaveConfigToBrowser();
    }
    public void RemoveLocalDecoupled(Integrator i, string path)
    {
        LocalDecoupledParameters[i]?.Remove(path);
        SyncGlobalSettings();
        _ = SaveConfigToBrowser();
    }

    // drag and panel state
    void HandleDragStart(Integrator item) => draggedItem = item;
    void HandleDrop(Integrator target) => draggedItem = null;
    void HandleDragEnter(Integrator target)
    {
        if (draggedItem == null || target == null || draggedItem == target) return;
        var oldIndex = addedIntegrators.IndexOf(draggedItem);
        var newIndex = addedIntegrators.IndexOf(target);
        if (oldIndex != newIndex)
        {
            addedIntegrators.RemoveAt(oldIndex);
            addedIntegrators.Insert(newIndex, draggedItem);
        }
    }
    bool IsExpanded(Integrator item) => expandedItems.Contains(item);
    void ToggleExpand(Integrator item)
    {
        if (!expandedItems.Remove(item)) expandedItems.Add(item);
    }

    // json and serialization
    async Task SaveConfigToBrowser()
    {
        var options = new JsonSerializerOptions {
            IncludeFields = true,
            WriteIndented = false
        };

        var snapshot = CreateConfigSnapshot();
        var jsonString = JsonSerializer.Serialize(snapshot, options); // 使用配置
        await JS.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, jsonString);
    }
    async Task LoadConfigFromBrowser()
    {
        var json = await JS.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
        if (!string.IsNullOrEmpty(json))
            LoadConfigFromSnapshot(JsonSerializer.Deserialize<IntegratorConfigDTO>(json));
    }
    async Task DownloadConfig()
    {
        var json = JsonSerializer.Serialize(CreateConfigSnapshot(), new JsonSerializerOptions { WriteIndented = true });
        using var streamRef = new DotNetStreamReference(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json)));
        await JS.InvokeVoidAsync("downloadFileFromStream", "seesharp-config.json", streamRef);
    }
    async Task UploadConfig(InputFileChangeEventArgs e)
    {
        if (e.File == null) return;
        using var reader = new StreamReader(e.File.OpenReadStream(1024 * 1024));
        var dto = JsonSerializer.Deserialize<IntegratorConfigDTO>(await reader.ReadToEndAsync());
        if (dto != null)
        {
            LoadConfigFromSnapshot(dto);
            await SaveConfigToBrowser();
        }
    }

    IntegratorConfigDTO CreateConfigSnapshot()
    {
        var dto = new IntegratorConfigDTO { DecoupledParameters = DecoupledParameters.ToList() };
        foreach (var p in IntegratorUtils.GetFilteredProps(typeof(Integrator)).Where(p => p.CanRead))
            dto.GlobalSettings[p.Name] = p.GetValue(globalIntegratorSettings)!;

        dto.Integrators = addedIntegrators.Select(i => {
            var state = new IntegratorStateDTO {
                AssemblyQualifiedName = i.GetType().AssemblyQualifiedName,
                CustomName = Names.GetValueOrDefault(i, ""),
                LocalDecoupledParameters = LocalDecoupledParameters.GetValueOrDefault(i)?.ToList() ?? new()
            };
            foreach (var group in IntegratorUtils.GetParameterGroups(i))
            {
                foreach (var p in group.Properties.Where(p => p.CanRead))
                    state.Parameters[p.Name] = p.GetValue(i)!;
                foreach (var f in group.Fields)
                    state.Parameters[f.Name] = f.GetValue(i)!;
            }
            return state;
        }).ToList();
        return dto;
    }

    void LoadConfigFromSnapshot(IntegratorConfigDTO dto)
    {
        addedIntegrators.Clear();
        Names.Clear();
        expandedItems.Clear();
        undoStack.Clear();
        LocalDecoupledParameters.Clear();
        DecoupledParameters = new HashSet<string>(dto.DecoupledParameters ?? new());

        foreach (var kvp in dto.GlobalSettings)
        {
            var prop = typeof(Integrator).GetProperty(kvp.Key);
            if (prop != null && prop.CanWrite)
                prop.SetValue(globalIntegratorSettings, ConvertJsonElement(kvp.Value, prop.PropertyType));
        }

        foreach (var state in dto.Integrators)
        {
            var type = Type.GetType(state.AssemblyQualifiedName);
            if (type == null) continue;
            var integrator = (Integrator)Activator.CreateInstance(type)!;

            foreach (var kvp in state.Parameters)
            {
                var prop = type.GetProperty(kvp.Key);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(integrator, ConvertJsonElement(kvp.Value, prop.PropertyType));
                    continue;
                }
                var field = type.GetField(kvp.Key);
                if (field != null)
                {
                    field.SetValue(integrator, ConvertJsonElement(kvp.Value, field.FieldType));
                }
            }
            addedIntegrators.Add(integrator);
            LocalDecoupledParameters[integrator] = new HashSet<string>(state.LocalDecoupledParameters ?? new());
            if (!string.IsNullOrEmpty(state.CustomName))
                Names[integrator] = state.CustomName;
            expandedItems.Add(integrator);
        }
        SyncGlobalSettings();
        StateHasChanged();
    }

    object? ConvertJsonElement(object? jsonValue, Type targetType)
    {
        if (jsonValue is JsonElement el)
        {
            var options = new JsonSerializerOptions { IncludeFields = true };
            return JsonSerializer.Deserialize(el.GetRawText(), targetType, options);
        }
        return jsonValue;
    }

    // helpers
    string GetNextAvailableName(string originalName)
    {
        string baseName = Regex.Replace(originalName, @"(?: \d+| - Copy)$", "");

        if (!Names.Values.Contains(baseName))
            return baseName;

        int counter = 2;
        while (Names.Values.Contains($"{baseName} {counter}"))
        {
            counter++;
        }
        return $"{baseName} {counter}";
    }

    protected List<ParameterGroup> GetParameterGroups(Integrator integrator) => IntegratorUtils.GetParameterGroups(integrator);
    protected string FormatClassName(Type t) => IntegratorUtils.FormatClassName(t);

    public List<ParameterGroup> GetGlobalParameterGroups()
    {
        if (addedIntegrators.Count <= 1)
        {
            return new List<ParameterGroup> {
                new ParameterGroup {
                    Title = "Global Settings",
                    IsGlobal = true,
                    Properties = IntegratorUtils.GetFilteredProps(typeof(Integrator)).ToList(),
                    Fields = IntegratorUtils.GetFilteredFields(typeof(Integrator)).ToList()
                }
            };
        }

        var allGroupSets = addedIntegrators.Select(i => {
            string leafTitle = FormatClassName(i.GetType());
            return GetParameterGroups(i).Where(g => g.Title != leafTitle).ToList();
        }).ToList();

        var commonTitles = allGroupSets[0]
            .Select(g => g.Title)
            .Where(title => allGroupSets.Skip(1).All(set => set.Any(g => g.Title == title)))
            .ToList();

        return allGroupSets[0].Where(g => commonTitles.Contains(g.Title)).ToList();
    }

    public void CleanUpInvalidDecoupledParameters()
    {
        var validGlobalTitles = GetGlobalParameterGroups().Select(g => g.Title).ToList();

        var invalidPaths = DecoupledParameters
            .Where(path => !validGlobalTitles.Contains(path.Split('.')[0]))
            .ToList();

        if (invalidPaths.Any())
        {
            foreach (var path in invalidPaths)
            {
                DecoupledParameters.Remove(path);
            }
            _ = SaveConfigToBrowser();
        }
    }

    public object? GetBulkValue(string title, string name)
    {
        var provider = addedIntegrators.FirstOrDefault(i => GetParameterGroups(i).Any(g => g.Title == title))
                       ?? globalIntegratorSettings;

        var group = GetParameterGroups(provider).FirstOrDefault(g => g.Title == title);
        if (group == null) return null;

        var prop = group.Properties.FirstOrDefault(p => p.Name == name);
        if (prop != null) return prop.GetValue(provider);

        var field = group.Fields.FirstOrDefault(f => f.Name == name);
        if (field != null) return field.GetValue(provider);

        return null;
    }

    public void SetBulkValue(string title, string name, object val)
    {
        string path = $"{title}.{name}";

        var tGroup = GetParameterGroups(globalIntegratorSettings).FirstOrDefault(g => g.Title == title);
        tGroup?.Properties.FirstOrDefault(p => p.Name == name)?.SetValue(globalIntegratorSettings, val);
        tGroup?.Fields.FirstOrDefault(f => f.Name == name)?.SetValue(globalIntegratorSettings, val);

        foreach (var i in addedIntegrators)
        {
            if (DecoupledParameters.Contains(path)) continue;
            if (LocalDecoupledParameters.TryGetValue(i, out var local) && local.Contains(path)) continue;

            var iGroup = GetParameterGroups(i).FirstOrDefault(g => g.Title == title);
            iGroup?.Properties.FirstOrDefault(p => p.Name == name)?.SetValue(i, val);
            iGroup?.Fields.FirstOrDefault(f => f.Name == name)?.SetValue(i, val);
        }

        _ = SaveConfigToBrowser();
        StateHasChanged();
    }

    public void ForceUpdateAndSave()
    {
        SyncGlobalSettings();
        _ = SaveConfigToBrowser();
        StateHasChanged();
    }

    public class IntegratorConfigDTO {
        public Dictionary<string, object> GlobalSettings { get; set; } = new();
        public List<string> DecoupledParameters { get; set; } = new();
        public List<IntegratorStateDTO> Integrators { get; set; } = new();
    }
    public class IntegratorStateDTO {
        public string? AssemblyQualifiedName { get; set; }
        public string? CustomName { get; set; }
        public List<string> LocalDecoupledParameters { get; set; } = new();
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
