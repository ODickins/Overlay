namespace Overlay;

public class OverlayAttribute : Attribute
{
    public bool CopyOnAdd { get; set; } = default!;
    public bool CopyOnModify { get; set; } = default!;
}