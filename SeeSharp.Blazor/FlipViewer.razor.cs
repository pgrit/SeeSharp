using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SeeSharp.Blazor;

public partial class FlipViewer(IJSRuntime JSRuntime) : ComponentBase
{
    string flipCode;
    string flipJson;

    [Parameter]
    public SimpleImageIO.FlipBook Flip { get; set; }
    SimpleImageIO.FlipBook lastFlip;

    /// <summary>
    /// Event arguments for flip book interactions
    /// </summary>
    /// <param name="MouseButton">The pressed mouse button, following the JS convention (0 left, 1 middle, 2 right, ...)</param>
    /// <param name="MouseX">Horizontal coordinate in image pixels (0, 0) is top left</param>
    /// <param name="MouseY">Horizontal coordinate in image pixels (0, 0) is top left</param>
    /// <param name="WheelDelta">Mouse wheel rotation</param>
    /// <param name="FlipbookID">ID of the flip book in the DOM</param>
    /// <param name="ActiveImage">0-based index of the current image in the flip book</param>
    /// <param name="KeysPressed">Contains the string "key" names (JS convention) of the pressed keys</param>
    public record struct OnEventArgs
    (
        int MouseButton,
        int MouseX,
        int MouseY,
        int WheelDelta,
        string FlipbookID,
        int ActiveImage,
        HashSet<String> KeysPressed
    )
    {
        public bool Control => KeysPressed.Contains("Control");
        public bool Alt => KeysPressed.Contains("Alt");
        public bool Shift => KeysPressed.Contains("Shift");
    }

    [Parameter]
    public EventCallback<OnEventArgs> OnClick { get; set; }
    [Parameter]
    public EventCallback<OnEventArgs> OnWheel { get; set; }
    [Parameter]
    public EventCallback<OnEventArgs> OnMouseOver { get; set; }
    [Parameter]
    public EventCallback<OnEventArgs> OnKey { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (Flip == null)
        {
            flipCode = null;
            lastFlip = null;
            return;
        }

        if (lastFlip == Flip) return;

        await Task.Run(() => {
            if (Flip.Count == 0) {
                flipJson = null;
                flipCode = "<p>empty flip book</p>";
                return;
            }
            var code = Flip.Generate();
            flipCode = code.Html;
            flipJson = code.Data;
        });

        lastFlip = Flip;
    }

    [JSInvokable]
    public void _OnFlipClick(int mouseButton, int mouseX, int mouseY, string ID, int selectedIdx, String[] keysPressed)
    {
        HashSet<string> set = new HashSet<string>(keysPressed);
        OnClick.InvokeAsync(new(MouseButton: mouseButton, MouseX: mouseX, MouseY: mouseY, WheelDelta: -1, FlipbookID: ID, ActiveImage: selectedIdx, KeysPressed: set)).Wait();
    }

    [JSInvokable]

    public void _OnFlipWheel(int mouseX, int mouseY, int deltaY, string ID, int selectedIdx, String[] keysPressed)
    {
        HashSet<string> set = new HashSet<string>(keysPressed);
        OnWheel.InvokeAsync(new(MouseButton: -1, MouseX: mouseX, MouseY: mouseY, WheelDelta: deltaY, FlipbookID: ID, ActiveImage: selectedIdx, KeysPressed: set)).Wait();
    }

    [JSInvokable]
    public void _OnFlipMouseOver(int mouseX, int mouseY, string ID, int selectedIdx, String[] keysPressed)
    {
        HashSet<string> set = new HashSet<string>(keysPressed);
        OnMouseOver.InvokeAsync(new(MouseButton: -1, MouseX: mouseX, MouseY: mouseY, WheelDelta: -1, FlipbookID: ID, ActiveImage: selectedIdx, KeysPressed: set)).Wait();
    }

    [JSInvokable]

    public void _OnFlipKey(int mouseX, int mouseY, string ID, int selectedIdx, String[] keysPressed)
    {
        HashSet<string> set = new HashSet<string>(keysPressed);
        OnKey.InvokeAsync(new(MouseButton: -1, MouseX: mouseX, MouseY: mouseY, WheelDelta: -1, FlipbookID: ID, ActiveImage: selectedIdx, KeysPressed: set)).Wait();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Need to wait with invoking the JS code until the HTML got added to the DOM on the client side
        if (flipJson != null)
        {
            await JSRuntime.InvokeVoidAsync(
                "makeFlipBook",
                flipJson,
                DotNetObjectReference.Create(this),
                nameof(_OnFlipClick),
                DotNetObjectReference.Create(this),
                nameof(_OnFlipWheel),
                DotNetObjectReference.Create(this),
                nameof(_OnFlipMouseOver),
                DotNetObjectReference.Create(this),
                nameof(_OnFlipKey)
                );
            flipJson = null;
        }
    }
}