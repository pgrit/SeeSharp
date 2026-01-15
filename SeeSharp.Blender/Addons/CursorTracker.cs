namespace SeeSharp.Blender
{
    public class CursorTrackerClient 
    {
        public readonly CursorTrackedHandler _cursor_tracked;
        public CursorTrackerClient(IEnumerable<IBlenderEventHandler> handlers)
        {
            _cursor_tracked = handlers
                .OfType<CursorTrackedHandler>()
                .Single(); // or First()
        } 
    }    
}
