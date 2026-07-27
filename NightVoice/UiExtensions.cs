using System;
using System.Windows.Forms;

namespace NightVoice;

internal static class UiExtensions
{
    public static IAsyncResult BeginInvoke(this Control control, Action action)
        => control.BeginInvoke((Delegate)action);
}
