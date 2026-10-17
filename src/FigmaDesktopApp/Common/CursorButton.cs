using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace FigmaDesktopApp.Common;

public class CursorButton : Button
{
    private static InputCursor? _handCursor;
    private static InputCursor HandCursor => _handCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Hand);

    public CursorButton()
    {
        SetHandCursor();
        Loaded += (s, e) => SetHandCursor();
        PointerEntered += (s, e) => SetHandCursor();
    }

    private void SetHandCursor()
    {
        try
        {
            ProtectedCursor = HandCursor;
        }
        catch
        {
            // Graceful fallback
        }
    }
}
