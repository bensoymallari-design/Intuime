using System.Runtime.InteropServices;
using Intuime.Core;
using Xunit;

namespace Intuime.Tests;

public class WebViewFaultTests
{
    [Fact]
    public void Scene_web_page_faults_stay_inside_the_app()
    {
        var focus = new ArgumentException("Value does not fall within the expected range.");
        Assert.False(WebViewFaults.IsWebFault(focus));
        Assert.True(WebViewFaults.IsWebFault(focus, pageOpen: true));
        Assert.True(WebViewFaults.IsFocusGlitch(focus));

        var move = new ArgumentException("Value does not fall within the expected range. NativeWebView.OnGotFocus MoveFocus");
        Assert.True(WebViewFaults.IsWebFault(move));
        Assert.True(WebViewFaults.IsFocusGlitch(move));

        var thread = new COMException("Cannot change thread mode after it is set. (0x80010106)");
        Assert.True(WebViewFaults.IsWebFault(thread));
        Assert.False(WebViewFaults.IsFocusGlitch(thread));

        var missing = new DllNotFoundException("libwebkit2gtk-4.1.so.0");
        Assert.True(WebViewFaults.IsWebFault(missing));
        Assert.False(WebViewFaults.IsFocusGlitch(missing));

        var runtime = new TypeLoadException("Webview2 runtime could not be loaded");
        Assert.True(WebViewFaults.IsWebFault(runtime));

        Assert.False(WebViewFaults.IsWebFault(new InvalidOperationException("scene was empty"), pageOpen: true));
        Assert.False(WebViewFaults.IsWebFault(null));
        Assert.False(WebViewFaults.IsFocusGlitch(null));
    }
}
