using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace FigmaDesktopApp.Common;

public class CursorButton : Button
{
    public CursorButton()
    {
        try
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }
        catch
        {
            // Graceful fallback
        }
    }
}
