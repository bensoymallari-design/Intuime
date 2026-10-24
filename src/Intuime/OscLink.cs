using System.Net;
using System.Net.Sockets;
using Avalonia.Threading;
using Intuime.Core;

namespace Intuime;

public sealed class OscLink : IDisposable
{
    private readonly PlaybackEngine _engine;
    private readonly UdpClient? _client;
    private readonly IPEndPoint? _destination;
    private readonly CancellationTokenSource _cancel = new();
    private int _disposed;

    public string? Problem { get; }

    public OscLink(PlaybackEngine engine, ExperienceDocument document)
    {
        _engine = engine;
        _engine.OscSend += Send;
        if (document.OscListenPort is > 0 and <= 65535)
        {
            try
            {
                _client = new UdpClient(document.OscListenPort);
                _ = Listen();
            }
            catch (SocketException ex)
            {
                Problem = "OSC port " + document.OscListenPort + " is already in use (" + ex.SocketErrorCode + ").";
            }
        }

        if (document.OscSendPort is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(document.OscSendHost))
        {
            try
            {
                var host = document.OscSendHost.Trim();
                if (!IPAddress.TryParse(host, out var address))
                {
                    var found = Dns.GetHostAddresses(host);
                    address = found.FirstOrDefault(item => item.AddressFamily == AddressFamily.InterNetwork) ?? found.FirstOrDefault();
                }
                if (address != null)
                    _destination = new IPEndPoint(address, document.OscSendPort);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                Problem ??= "OSC could not find " + document.OscSendHost + ".";
            }
        }

        if (_client == null && _destination != null && Problem == null)
            _client = new UdpClient(0);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _engine.OscSend -= Send;
        _cancel.Cancel();
        _client?.Dispose();
        _cancel.Dispose();
    }

    private async Task Listen()
    {
        var client = _client;
        if (client == null)
            return;
        while (!_cancel.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await client.ReceiveAsync(_cancel.Token);
            }
            catch (Exception)
            {
                break;
            }
            var messages = OscCodec.Decode(result.Buffer);
            if (messages.Count == 0)
                continue;
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed != 0)
                    return;
                foreach (var message in messages)
                    _engine.ReceiveOsc(message);
            });
        }
    }

    private void Send(OscMessage message)
    {
        if (_disposed != 0 || _client == null || _destination == null)
            return;
        var bytes = OscCodec.Encode(message);
        if (bytes.Length == 0)
            return;
        try
        {
            _client.Send(bytes, bytes.Length, _destination);
        }
        catch (SocketException)
        {
        }
    }
}
