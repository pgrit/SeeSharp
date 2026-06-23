namespace SeeSharp.Blender;

public class CursorTrackerClient {
    public readonly CursorTrackedHandler _cursorTracked;
    public CursorTrackerClient(IEnumerable<IBlenderEventHandler> handlers) {
        _cursorTracked = handlers
            .OfType<CursorTrackedHandler>()
            .Single(); 
    } 
}    