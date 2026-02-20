using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;
using SeeSharp.Integrators;
using SeeSharp.Common;
using System.Text.Json;
using System.Text.RegularExpressions;

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

    HashSet<Integrator> expandedItems = new();
    HashSet<Integrator> mutedIntegrators = new();
    Dictionary<Integrator, Integrator> globalSettingsBackup = new();
    Integrator? draggedItem;

    Type[] integratorTypes = Array.Empty<Type>();
    string? selectedIntegrator;
    private Stack<(Integrator Ptr, int Index, bool IsMuted, string? Name, Integrator? Backup, bool IsExpanded)> undoStack = new();
    private static (string GroupTitle, Dictionary<string, object> Values)? settingsClipboard;
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

        if (integratorTypes.Length > 0) {
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

    // integrator management
    void OnSelectionChanged(ChangeEventArgs e) => selectedIntegrator = e.Value?.ToString();

    void AddIntegrator()
    {
        if (string.IsNullOrEmpty(selectedIntegrator)) return;
        var type = integratorTypes.FirstOrDefault(t => t.FullName == selectedIntegrator);
        if (type == null) return;

        var integrator = (Integrator)Activator.CreateInstance(type)!;
        ApplyGlobalSettings(integrator);
        Names[integrator] = FormatClassName(type);
        mutedIntegrators.Add(integrator);
        addedIntegrators.Add(integrator);
        expandedItems.Add(integrator);

        _ = SaveConfigToBrowser();
    }

    void RunIntegrator(Integrator integrator)
    {
        _ = SaveConfigToBrowser();
        if (IsGlobalMuted(integrator))
            ApplyGlobalSettings(integrator);
        OnRunIntegrator.InvokeAsync(integrator);
    }

    void RunAllIntegrators()
    {
        _ = SaveConfigToBrowser();
        foreach (var integrator in addedIntegrators.Where(IsGlobalMuted))
            ApplyGlobalSettings(integrator);
        OnRunAllIntegrators.InvokeAsync();
    }

    void DeleteIntegrator(Integrator integrator)
    {
        undoStack.Push((integrator, addedIntegrators.IndexOf(integrator), mutedIntegrators.Contains(integrator),
                        Names.GetValueOrDefault(integrator), globalSettingsBackup.GetValueOrDefault(integrator), expandedItems.Contains(integrator)));

        addedIntegrators.Remove(integrator);
        mutedIntegrators.Remove(integrator);
        globalSettingsBackup.Remove(integrator);
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

            if (state.IsMuted) mutedIntegrators.Add(state.Ptr);
            if (state.IsExpanded) expandedItems.Add(state.Ptr);

            if (!string.IsNullOrEmpty(state.Name))
                Names[state.Ptr] = state.Name;

            if (state.Backup != null)
                globalSettingsBackup[state.Ptr] = state.Backup;
        }
    }

    void DuplicateIntegrator(Integrator source)
    {
        var type = source.GetType();
        var newIntegrator = (Integrator)Activator.CreateInstance(type)!;
        CopyAllProperties(source, newIntegrator);

        string sourceName = Names.GetValueOrDefault(source, FormatClassName(type));
        Names[newIntegrator] = GetNextAvailableName(sourceName);

        addedIntegrators.Add(newIntegrator);
        if (mutedIntegrators.Contains(source))
        {
            mutedIntegrators.Add(newIntegrator);
            if (globalSettingsBackup.TryGetValue(source, out var backup))
            {
                var backupClone = (Integrator)Activator.CreateInstance(type)!;
                CopyAllProperties(backup, backupClone);
                globalSettingsBackup[newIntegrator] = backupClone;
            }
        }

        expandedItems.Add(newIntegrator);
    }

    // drag and panel state
    void HandleDragStart(Integrator item) => draggedItem = item;
    void HandleDrop(Integrator target) => draggedItem = null;
    void HandleDragEnter(Integrator target)
    {
        if (draggedItem == null || target == null || draggedItem == target) return;
        var oldIndex = addedIntegrators.IndexOf(draggedItem);
        var newIndex = addedIntegrators.IndexOf(target);
        if (oldIndex != newIndex) {
            addedIntegrators.RemoveAt(oldIndex);
            addedIntegrators.Insert(newIndex, draggedItem);
        }
    }

    bool IsExpanded(Integrator item) => expandedItems.Contains(item);
    void ToggleExpand(Integrator item)
    {
        if (!expandedItems.Remove(item))
            expandedItems.Add(item);
    }

    bool IsGlobalMuted(Integrator item) => mutedIntegrators.Contains(item);
    void ToggleGlobalMute(Integrator item)
    {
        if (mutedIntegrators.Contains(item)) {
            RestoreGlobalSettings(item);
            mutedIntegrators.Remove(item);
        }
        else {
            BackupGlobalSettings(item);
            mutedIntegrators.Add(item);
        }
    }

    // copy and paste
    void CopyGroupSettings(object targetObj, ParameterGroup group)
    {
        var values = new Dictionary<string, object>();

        foreach (var prop in group.Properties)
            values[prop.Name] = prop.GetValue(targetObj);
        foreach (var field in group.Fields)
            values[field.Name] = field.GetValue(targetObj);

        settingsClipboard = (group.Title, values);
    }

    void PasteGroupSettings(object targetObj, ParameterGroup group)
    {
        if (settingsClipboard.Value.GroupTitle != group.Title)
            return;

        var values = settingsClipboard.Value.Values;

        foreach (var prop in group.Properties)
        {
            if (values.TryGetValue(prop.Name, out var val))
                prop.SetValue(targetObj, val);
        }
        foreach (var field in group.Fields)
        {
            if (values.TryGetValue(field.Name, out var val))
                field.SetValue(targetObj, val);
        }
    }

    void CopyAllProperties(Integrator source, Integrator target)
    {
        foreach (var group in IntegratorUtils.GetParameterGroups(source))
        {
            foreach (var prop in group.Properties.Where(p => p.CanRead && p.CanWrite))
                prop.SetValue(target, prop.GetValue(source));
            foreach (var field in group.Fields)
                field.SetValue(target, field.GetValue(source));
        }
    }

    void ApplyGlobalSettings(Integrator integrator) => CopyGlobalProperties(globalIntegratorSettings, integrator);

    void BackupGlobalSettings(Integrator integrator)
    {
        var backup = (Integrator)Activator.CreateInstance(integrator.GetType())!;
        CopyGlobalProperties(integrator, backup);
        globalSettingsBackup[integrator] = backup;
    }

    void RestoreGlobalSettings(Integrator integrator)
    {
        if (globalSettingsBackup.TryGetValue(integrator, out var backup))
        {
            CopyGlobalProperties(backup, integrator);
            globalSettingsBackup.Remove(integrator);
        }
    }

    void CopyGlobalProperties(Integrator source, Integrator target)
    {
        var props = IntegratorUtils.GetFilteredProps(typeof(Integrator));

        foreach (var prop in props) {
            if (prop.CanRead && prop.CanWrite)
            {
                prop.SetValue(target, prop.GetValue(source));
            }
        }
    }

    // import and output config
    async Task SaveConfigToBrowser() => await JS.InvokeVoidAsync("localStorage.setItem", LocalStorageKey, JsonSerializer.Serialize(CreateConfigSnapshot()));

    async Task LoadConfigFromBrowser()
    {
        var json = await JS.InvokeAsync<string>("localStorage.getItem", LocalStorageKey);
        if (!string.IsNullOrEmpty(json))
        {
            LoadConfigFromSnapshot(JsonSerializer.Deserialize<IntegratorConfigDTO>(json));
        }
    }

    async Task DownloadConfig()
    {
        var dto = CreateConfigSnapshot();
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        var fileBytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream(fileBytes);
        using var streamRef = new DotNetStreamReference(stream);
        await JS.InvokeVoidAsync("downloadFileFromStream", "seesharp-config.json", streamRef);
    }

    async Task UploadConfig(InputFileChangeEventArgs e)
    {
        if (e.File == null) return;
        using var reader = new StreamReader(e.File.OpenReadStream(1024 * 1024));
        var dto = JsonSerializer.Deserialize<IntegratorConfigDTO>(await reader.ReadToEndAsync());
        if (dto != null)
        {
            LoadConfigFromSnapshot(dto); await SaveConfigToBrowser();
        }
    }

    IntegratorConfigDTO CreateConfigSnapshot()
    {
        var dto = new IntegratorConfigDTO();

        foreach (var p in IntegratorUtils.GetFilteredProps(typeof(Integrator)).Where(p => p.CanRead))
            dto.GlobalSettings[p.Name] = p.GetValue(globalIntegratorSettings)!;

        dto.Integrators = addedIntegrators.Select(i => {
            var state = new IntegratorStateDTO {
                AssemblyQualifiedName = i.GetType().AssemblyQualifiedName,
                CustomName = Names.GetValueOrDefault(i, ""),
                IsMuted = mutedIntegrators.Contains(i)
            };
            foreach (var group in IntegratorUtils.GetParameterGroups(i)) {
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
        mutedIntegrators.Clear();
        Names.Clear();
        expandedItems.Clear();
        globalSettingsBackup.Clear();
        undoStack.Clear();

        foreach (var kvp in dto.GlobalSettings)
        {
            var prop = typeof(Integrator).GetProperty(kvp.Key);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(globalIntegratorSettings, ConvertJsonElement(kvp.Value, prop.PropertyType));
            }
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
            if (!string.IsNullOrEmpty(state.CustomName))
                Names[integrator] = state.CustomName;
            if (state.IsMuted)
            {
                mutedIntegrators.Add(integrator);
                BackupGlobalSettings(integrator);
            }
            expandedItems.Add(integrator);
        }
        StateHasChanged();
    }

    object? ConvertJsonElement(object? jsonValue, Type targetType) => jsonValue is JsonElement el ? JsonSerializer.Deserialize(el.GetRawText(), targetType) : jsonValue;

    // helpers
    string GetNextAvailableName(string originalName)
    {
        string baseName = Regex.Match(originalName, @"^(.*?) - Copy(?: (\d+))?$").Success ? Regex.Match(originalName, @"^(.*?) - Copy(?: (\d+))?$").Groups[1].Value : originalName;
        int counter = 1;
        while (Names.Values.Any(n => n == (counter == 1 ? $"{baseName} - Copy" : $"{baseName} - Copy {counter}")))
            counter++;
        return counter == 1 ? $"{baseName} - Copy" : $"{baseName} - Copy {counter}";
    }

    protected List<ParameterGroup> GetParameterGroups(Integrator integrator)
        => IntegratorUtils.GetParameterGroups(integrator);

    protected string FormatClassName(Type t) => IntegratorUtils.FormatClassName(t);

    public class IntegratorConfigDTO
    {
        public Dictionary<string, object> GlobalSettings { get; set; } = new();
        public List<IntegratorStateDTO> Integrators { get; set; } = new();
    }
    public class IntegratorStateDTO
    {
        public string? AssemblyQualifiedName { get; set; }
        public string? CustomName { get; set; }
        public bool IsMuted { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}
