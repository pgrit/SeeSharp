using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SeeSharp.Blazor;

namespace SeeSharp.Blazor.Template.Pages;

public struct ListenerState {
    public ListenerState() { }

    /// <summary>
    /// The index of the selected image of the current selected flipbook (selected by clicking on it)
    /// </summary>
    public int selectedIndex = 0;

    /// <summary>
    /// Number between 0 and NumSamples. Can be used if data is stored from different iterations
    /// </summary>
    public int currIteration = 0;

    // Add here all action keys for your functionalities
    // Attention: any key press disables the default html scrolling!
    public string actionKey1 = "a";
    public string actionKey2 = "d";
    public bool actionKey1Pressed = false;
    public bool actionKey2Pressed = false;

    public int currX = 0;
    public int currY = 0;

    /// <summary>
    /// The ID of the current flipbook
    /// </summary>
    public string currFlipID = "";
}

/// <summary>
/// Differences between event type so update methods for flipbooks can ignore events
/// </summary>
public enum FiredType {
    Click = 0,
    Move = 1,
    Wheel = 2,
    Key = 4,
}

public partial class BaseExperiment : ComponentBase {
    protected const int Width = 1280;
    protected const int Height = 720;
    protected const int FlipWidth = 660;
    protected const int FlipHeight = 580;
    protected const int MaxDepth = 10;
    public int NumSamples = 2;

    /// <summary>
    /// Registers all Flipbooks
    /// Key will be the stringified key of a Flipbook which is set by Flipbook.SetKey
    /// A Flipbook key is an array of ints
    /// </summary>
    protected Dictionary<string, FlipBook> registry = new Dictionary<string, FlipBook>();
    protected ListenerState state = new ListenerState();
    public static string Reverse(string s) {
        char[] charArray = s.ToCharArray();
        Array.Reverse(charArray);
        return new string(charArray);
    }

    /// <summary>
    /// Is fired when clicked on an image in the flipbook
    /// </summary>
    /// <param name="args">ListenerState from HMTL side</param>
    public virtual void OnFlipClick(FlipViewer.OnEventArgs args) {
        updateFlipbook(FiredType.Click);
    }

    /// <summary>
    /// Is fired when the mouse wheel state changes over an image. 
    /// This gets only called when the alt key is pressed (from HTML side)
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    public virtual void OnFlipWheel(FlipViewer.OnEventArgs args) {
        if (state.actionKey1Pressed) {
            // scrolled up
            if (args.deltaY < 0) {
                if (state.currIteration < NumSamples - 1) {
                    state.currIteration++;
                    updateFlipbook(FiredType.Wheel);
                }
            }
            // scrolled down
            if (args.deltaY >= 0) {
                if (state.currIteration > 0) {
                    state.currIteration--;
                    updateFlipbook(FiredType.Wheel);
                }
            }
        }
    }

    /// <summary>
    /// Is fired when mouse position changes over the selected flipbook
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    public virtual void OnFlipMouseOver(FlipViewer.OnEventArgs args) {
        if (state.currX == args.mouseX && state.currY == args.mouseY)
            return;

        if (args.mouseX >= 0 && args.mouseX <= Width - 1)
            state.currX = args.mouseX;
        if (args.mouseY >= 0 && args.mouseY <= Height - 1)
            state.currY = args.mouseY;

        updateFlipbook(FiredType.Move);
    }

    /// <summary>
    /// Is fired when key is pressed or released
    /// </summary>
    /// <param name="args">ListenerState from HMTL side.</param>
    public virtual void OnFlipKey(FlipViewer.OnEventArgs args) {
        bool wasKey1Pressed = state.actionKey1Pressed;

        state.actionKey1Pressed = args.keysPressed.Contains(state.actionKey1);
        state.actionKey2Pressed = args.keysPressed.Contains(state.actionKey2);

        state.currFlipID = args.FlipbookID;
        state.selectedIndex = args.selectedIndex;

        if (!state.actionKey1Pressed && wasKey1Pressed)
            state.currIteration = 0;

        updateFlipbook(FiredType.Key);
    }

    /// <summary>
    /// Catches fired events and forward events to selected flipbooks
    /// </summary>
    /// <param name="fired">Fired type</param>
    public virtual void updateFlipbook(FiredType fired) {
        if (String.IsNullOrEmpty(state.currFlipID))
            return;
    }

    /// <summary>
    /// Runs the experiment when "..." is pressed on the HTML
    /// </summary>
    public virtual void RunExperiment() {
    }
    
    /// <summary>
    /// Is executed when Download button is pressed on the HTML
    /// </summary>
    /// <returns></returns>
    public virtual async Task OnDownloadClick() {
    }
}