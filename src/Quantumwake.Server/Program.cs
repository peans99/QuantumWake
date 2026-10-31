using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;
using Quantumwake.Server;

// Entry point for the standalone server. Everything it does lives in
// ServerHost, which the overlay shell starts in-process instead - see
// ServerHost for why.

// A second copy. The desktop shell tells the pilot and opens the running one;
// a terminal used to get Kestrel's stack trace. Asking the port first keeps
// the host from logging that failure at all, and the catch below covers the
// race where the other copy appears between the probe and the bind.
var port = PortFromArguments(args);
if (SomethingAnswersOn(port))
    return AlreadyRunning(port);

try
{
    ServerHost.Build(args).Run();
}
catch (IOException e) when (e.InnerException is AddressInUseException)
{
    return AlreadyRunning(port);
}
return 0;

static int AlreadyRunning(int port)
{
    Console.Error.WriteLine(
        $"Quantum Wake is already running at http://127.0.0.1:{port} - open that one, or start this copy with another --Port.");
    return 1;
}

// --Port 31395 and --Port=31395, the two spellings the configuration binder
// accepts; anything else is the default the server itself falls back to.
static int PortFromArguments(string[] args)
{
    for (var i = 0; i < args.Length; i++)
    {
        var a = args[i];
        if (a.Equals("--Port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length
            && int.TryParse(args[i + 1], out var next))
            return next;
        if (a.StartsWith("--Port=", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(a["--Port=".Length..], out var inline))
            return inline;
    }
    return 31337;
}

static bool SomethingAnswersOn(int port)
{
    try
    {
        using var probe = new TcpClient();
        return probe.ConnectAsync("127.0.0.1", port).Wait(TimeSpan.FromMilliseconds(300)) && probe.Connected;
    }
    catch
    {
        return false; // refused or timed out: the port is free, or close enough
    }
}
