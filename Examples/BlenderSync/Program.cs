using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

ProgressBar.Silent = true;
SceneRegistry.AddSourceRelativeToScript("../../Data/Scenes");

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddSingleton<SeeSharp.Blender.IBlenderEventHandler, CreatedHandler>();
builder.Services.AddSingleton<SeeSharp.Blender.IBlenderEventHandler, DeletedHandler>();
builder.Services.AddSingleton<SeeSharp.Blender.IBlenderEventHandler, SelectedHandler>();
builder.Services.AddSingleton<SeeSharp.Blender.IBlenderEventHandler, CursorTrackedHandler>();


builder.Services.AddSingleton<SeeSharp.Blender.BlenderCommandSender>();
builder.Services.AddSingleton<SeeSharp.Blender.BlenderEventDispatcher>();
builder.Services.AddSingleton<SeeSharp.Blender.BlenderEventListener>();


builder.Services.AddSingleton<SeeSharp.Blender.PathViewerClient>();
builder.Services.AddSingleton<SeeSharp.Blender.CursorTrackerClient>();

var app = builder.Build();

var eventListener = app.Services.GetRequiredService<SeeSharp.Blender.BlenderEventListener>();
_ = eventListener.StartAsync();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();