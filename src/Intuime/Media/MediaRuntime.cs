using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Intuime.Media;

public static class MediaRuntime
{
    private static LibVLC? _vlc;
    private static bool _tried;
    private static bool _resolverReady;
    private static string _status = "Media engine has not started.";

    public static bool IsAvailable { get; private set; }
    public static string Status => _status;
    public static LibVLC? Shared => Ensure();

    public static LibVLC? Ensure()
    {
        if (_tried)
            return _vlc;
        _tried = true;
        try
        {
            PrepareLinuxLibraries();
            LibVLCSharp.Shared.Core.Initialize();
            var options = new List<string> { "--no-video-title-show", "--quiet" };
            if (OperatingSystem.IsLinux())
            {
                // Embed into the player window. OpenGL outputs stay black on a software display.
                options.Add("--vout=xcb_x11");
                options.Add("--avcodec-hw=none");
            }
            if (!HasAudioDevice())
                options.Add("--aout=adummy");
            _vlc = new LibVLC(options.ToArray());
            IsAvailable = true;
            _status = "LibVLC is ready.";
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            _status = "Video and audio need VLC. The Windows build includes it.";
            Console.Error.WriteLine("LibVLC failed to start: " + ex.Message);
        }
        return _vlc;
    }

    private static void PrepareLinuxLibraries()
    {
        if (!OperatingSystem.IsLinux() || _resolverReady)
            return;
        _resolverReady = true;
        var plugins = LinuxLibVlcDirectory();
        if (plugins != null)
        {
            var pluginPath = Path.Combine(plugins, "vlc", "plugins");
            if (Directory.Exists(pluginPath))
                Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
        }

        NativeLibrary.SetDllImportResolver(typeof(LibVLC).Assembly, (libraryName, _, _) =>
        {
            if (!libraryName.Contains("vlc", StringComparison.OrdinalIgnoreCase))
                return IntPtr.Zero;
            var file = libraryName.Contains("core", StringComparison.OrdinalIgnoreCase)
                ? "libvlccore.so.9"
                : "libvlc.so.5";
            foreach (var directory in LinuxLibraryDirectories())
            {
                var path = Path.Combine(directory, file);
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
                    return handle;
            }
            return IntPtr.Zero;
        });
    }

    private static bool HasAudioDevice()
    {
        if (!OperatingSystem.IsLinux())
            return true;
        try
        {
            if (!File.Exists("/proc/asound/cards"))
                return false;
            var text = File.ReadAllText("/proc/asound/cards");
            return text.Contains('[', StringComparison.Ordinal) &&
                   !text.Contains("no soundcards", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? LinuxLibVlcDirectory() =>
        LinuxLibraryDirectories().FirstOrDefault(directory => File.Exists(Path.Combine(directory, "libvlc.so.5")));

    private static IEnumerable<string> LinuxLibraryDirectories()
    {
        var arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "aarch64-linux-gnu",
            Architecture.X64 => "x86_64-linux-gnu",
            _ => null
        };
        if (arch != null)
            yield return "/usr/lib/" + arch;
        yield return "/usr/lib64";
        yield return "/usr/lib";
    }
}
