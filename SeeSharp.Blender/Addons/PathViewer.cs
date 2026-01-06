namespace SeeSharp.Blender
{
    public class PathViewerClient 
    {
        public readonly CreatedHandler _created;
        public readonly DeletedHandler _deleted;
        public readonly SelectedHandler _selected;

        public PathViewerClient(IEnumerable<IBlenderEventHandler> handlers)
        {
            _created = handlers
                .OfType<CreatedHandler>()
                .Single(); // or First()
            _deleted = handlers
                .OfType<DeletedHandler>()
                .Single(); 
            _selected = handlers
                .OfType<SelectedHandler>()
                .Single(); 
        } 
    }    
}
