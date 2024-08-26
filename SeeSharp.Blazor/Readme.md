Reusable Razor components for SeeSharp.

The following must be added to the `<head>` part of _Host.cshtml for the FlipBook integration to work:
```html
@Html.Raw(SeeSharp.Blazor.Scripts.FlipBookScript)
```