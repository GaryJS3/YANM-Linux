using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace YANM_Linux;

internal static class Program
{
    private static int Main(string[] args)
    {
        var networkService = new NetworkService();
        var commandRunner = new CommandRunner();

        if (args.Length == 0)
        {
            new ConsoleUi(networkService, commandRunner).Run();
            return 0;
        }

        return args[0] switch
        {
            "--help" or "-h" or "help" or "--no-ui" => PrintHelp(),
            "list" => PrintList(networkService),
            "show" when args.Length == 2 => PrintDetail(networkService, args[1]),
            "show" => PrintUsageError("show requires an interface name."),
            _ => PrintUsageError($"Unknown command: {args[0]}")
        };
    }

    private static int PrintHelp()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  netutil                 Start interactive UI");
        Console.WriteLine("  netutil --help          Print help");
        Console.WriteLine("  netutil --no-ui         Print help and exit");
        Console.WriteLine("  netutil list            Print interface list");
        Console.WriteLine("  netutil show <iface>    Print interface detail");
        return 0;
    }

    private static int PrintUsageError(string message)
    {
        Console.Error.WriteLine(message);
        PrintHelp();
        return 1;
    }

    private static int PrintList(NetworkService networkService)
    {
        foreach (var iface in networkService.GetInterfaces())
        {
            Console.WriteLine($"{iface.Name}\tstate={iface.OperState}\tcarrier={iface.Carrier}\tipv4={iface.IPv4Address}\tspeed={iface.Speed}\tmanager={iface.Manager}");
        }

        return 0;
    }

    private static int PrintDetail(NetworkService networkService, string name)
    {
        var detail = networkService.GetInterfaceDetail(name);
        if (detail is null)
        {
            Console.Error.WriteLine($"Interface not found: {name}");
            return 1;
        }

        Console.WriteLine($"Name: {detail.Name}");
        Console.WriteLine($"MAC address: {detail.MacAddress}");
        Console.WriteLine($"MTU: {detail.Mtu}");
        Console.WriteLine($"IPv4 addresses: {FormatList(detail.IPv4Addresses)}");
        Console.WriteLine($"IPv6 addresses: {FormatList(detail.IPv6Addresses)}");
        Console.WriteLine($"Default route: {detail.DefaultRoute}");
        Console.WriteLine($"Driver: {detail.Driver}");
        Console.WriteLine($"Firmware: {detail.Firmware}");
        Console.WriteLine($"Bus info: {detail.BusInfo}");
        Console.WriteLine($"Speed: {detail.Speed}");
        Console.WriteLine($"Duplex: {detail.Duplex}");
        Console.WriteLine($"Autonegotiation: {detail.Autonegotiation}");
        Console.WriteLine($"Manager: {detail.Manager}");
        return 0;
    }

    private static string FormatList(IReadOnlyList<string> values) => values.Count == 0 ? "unknown" : string.Join(", ", values);
}

internal sealed class ConsoleUi(NetworkService networkService, CommandRunner commandRunner)
{
    public void Run()
    {
        while (true)
        {
            Console.Clear();
            var interfaces = networkService.GetInterfaces();
            Console.WriteLine("Network interfaces");
            Console.WriteLine();

            if (interfaces.Count == 0)
            {
                Console.WriteLine("No network interfaces detected.");
                ReadKey("Press any key to quit.");
                return;
            }

            for (var i = 0; i < interfaces.Count; i++)
            {
                var iface = interfaces[i];
                Console.WriteLine($"{i + 1}. {iface.Name}  state={iface.OperState}  carrier={iface.Carrier}  ipv4={iface.IPv4Address}  speed={iface.Speed}  manager={iface.Manager}");
            }

            Console.WriteLine();
            Console.Write("Select interface number, R to refresh, or Q to quit: ");
            var input = Console.ReadLine()?.Trim();

            if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(input, out var selected) && selected >= 1 && selected <= interfaces.Count)
            {
                ShowInterface(interfaces[selected - 1].Name);
            }
        }
    }

    private void ShowInterface(string name)
    {
        while (true)
        {
            Console.Clear();
            var detail = networkService.GetInterfaceDetail(name);
            if (detail is null)
            {
                Console.WriteLine($"Interface not found: {name}");
                ReadKey("Press any key to return.");
                return;
            }

            PrintDetail(detail);
            Console.WriteLine();
            Console.WriteLine("Actions:");
            Console.WriteLine("1. Refresh");
            Console.WriteLine("2. Bring interface up");
            Console.WriteLine("3. Bring interface down");
            Console.WriteLine("4. Renew DHCP");
            Console.WriteLine("5. Set DHCP");
            Console.WriteLine("6. Set static IPv4");
            Console.WriteLine("7. Back");
            Console.WriteLine("8. Quit");
            Console.Write("Select action: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1":
                    continue;
                case "2":
                    ApplyChangingAction("Bring interface up", CommandPlan.Single("ip", "link", "set", "dev", name, "up"));
                    break;
                case "3":
                    ApplyChangingAction("Bring interface down", CommandPlan.Single("ip", "link", "set", "dev", name, "down"));
                    break;
                case "4":
                    ApplyChangingAction("Renew DHCP", new CommandPlan([
                        new BackendCommand("dhclient", ["-r", name]),
                        new BackendCommand("dhclient", [name])
                    ]));
                    break;
                case "5":
                    ApplyChangingAction("Set DHCP", CommandPlan.Single("dhclient", name));
                    break;
                case "6":
                    var plan = PromptStaticIPv4Plan(name);
                    if (plan is not null)
                    {
                        ApplyChangingAction("Set static IPv4", plan);
                    }
                    break;
                case "7":
                    return;
                case "8":
                    Environment.Exit(0);
                    return;
            }
        }
    }

    private static void PrintDetail(InterfaceDetail detail)
    {
        Console.WriteLine(detail.Name);
        Console.WriteLine($"MAC address: {detail.MacAddress}");
        Console.WriteLine($"MTU: {detail.Mtu}");
        Console.WriteLine($"IPv4 addresses: {FormatList(detail.IPv4Addresses)}");
        Console.WriteLine($"IPv6 addresses: {FormatList(detail.IPv6Addresses)}");
        Console.WriteLine($"Default route: {detail.DefaultRoute}");
        Console.WriteLine($"Driver: {detail.Driver}");
        Console.WriteLine($"Firmware: {detail.Firmware}");
        Console.WriteLine($"Bus info: {detail.BusInfo}");
        Console.WriteLine($"Speed: {detail.Speed}");
        Console.WriteLine($"Duplex: {detail.Duplex}");
        Console.WriteLine($"Autonegotiation: {detail.Autonegotiation}");
        Console.WriteLine($"Manager: {detail.Manager}");
    }

    private static CommandPlan? PromptStaticIPv4Plan(string name)
    {
        Console.Write("IP/CIDR (example 192.168.1.50/24): ");
        var ipCidr = Console.ReadLine()?.Trim();
        Console.Write("Gateway: ");
        var gateway = Console.ReadLine()?.Trim();
        Console.Write("DNS servers (space-separated): ");
        var dnsServers = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(ipCidr) || string.IsNullOrWhiteSpace(gateway))
        {
            Console.WriteLine("IP/CIDR and gateway are required.");
            ReadKey("Press any key to continue.");
            return null;
        }

        var commands = new List<BackendCommand>
        {
            new("ip", ["addr", "flush", "dev", name]),
            new("ip", ["addr", "add", ipCidr, "dev", name]),
            new("ip", ["route", "replace", "default", "via", gateway, "dev", name])
        };

        var dns = SplitWords(dnsServers);
        if (dns.Count > 0)
        {
            commands.Add(new BackendCommand("resolvectl", ["dns", name, .. dns]));
        }

        return new CommandPlan(commands);
    }

    private void ApplyChangingAction(string title, CommandPlan plan)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        Console.WriteLine("Backend command(s):");
        foreach (var command in plan.Commands)
        {
            Console.WriteLine($"  {command.DisplayText}");
        }

        Console.Write("Apply this change? Type yes to continue: ");
        if (!string.Equals(Console.ReadLine()?.Trim(), "yes", StringComparison.Ordinal))
        {
            Console.WriteLine("No changes made.");
            ReadKey("Press any key to continue.");
            return;
        }

        if (!CommandRunner.IsRoot())
        {
            Console.WriteLine("Not running as root. No changes made.");
            Console.WriteLine("Run equivalent command(s):");
            foreach (var command in plan.Commands)
            {
                Console.WriteLine($"  sudo {command.DisplayText}");
            }
            ReadKey("Press any key to continue.");
            return;
        }

        foreach (var command in plan.Commands)
        {
            var result = commandRunner.Run(command.FileName, command.Arguments);
            if (result.ExitCode != 0)
            {
                Console.WriteLine($"Command failed ({result.ExitCode}): {command.DisplayText}");
                if (!string.IsNullOrWhiteSpace(result.Error))
                {
                    Console.WriteLine(result.Error.Trim());
                }
                ReadKey("Press any key to continue.");
                return;
            }
        }

        Console.WriteLine("Change applied.");
        ReadKey("Press any key to continue.");
    }

    private static IReadOnlyList<string> SplitWords(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatList(IReadOnlyList<string> values) => values.Count == 0 ? "unknown" : string.Join(", ", values);

    private static void ReadKey(string message)
    {
        Console.WriteLine(message);
        Console.ReadKey(intercept: true);
    }
}

internal sealed class NetworkService
{
    private readonly CommandRunner commandRunner = new();

    public IReadOnlyList<InterfaceSummary> GetInterfaces()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .OrderBy(interfaceInfo => interfaceInfo.Name, StringComparer.Ordinal)
            .Select(interfaceInfo =>
            {
                var name = interfaceInfo.Name;
                return new InterfaceSummary(
                    name,
                    ReadSysClassNet(name, "operstate"),
                    ReadCarrier(name),
                    GetIPv4Addresses(interfaceInfo).FirstOrDefault() ?? "none",
                    ReadSpeed(name),
                    DetectManager(name));
            })
            .ToList();
    }

    public InterfaceDetail? GetInterfaceDetail(string name)
    {
        var interfaceInfo = NetworkInterface.GetAllNetworkInterfaces()
            .FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));

        if (interfaceInfo is null)
        {
            return null;
        }

        var driverInfo = GetDriverInfo(name);
        var linkInfo = GetLinkInfo(name);

        return new InterfaceDetail(
            name,
            FormatMac(interfaceInfo.GetPhysicalAddress()),
            ReadSysClassNet(name, "mtu"),
            GetIPAddresses(interfaceInfo, AddressFamily.InterNetwork),
            GetIPAddresses(interfaceInfo, AddressFamily.InterNetworkV6),
            GetDefaultRoute(name),
            GetValue(driverInfo, "driver"),
            GetValue(driverInfo, "firmware-version"),
            GetValue(driverInfo, "bus-info"),
            GetValue(linkInfo, "Speed", ReadSpeed(name)),
            GetValue(linkInfo, "Duplex"),
            GetValue(linkInfo, "Auto-negotiation"),
            DetectManager(name));
    }

    private static IReadOnlyList<string> GetIPv4Addresses(NetworkInterface interfaceInfo) => GetIPAddresses(interfaceInfo, AddressFamily.InterNetwork);

    private static IReadOnlyList<string> GetIPAddresses(NetworkInterface interfaceInfo, AddressFamily addressFamily)
    {
        return interfaceInfo.GetIPProperties().UnicastAddresses
            .Where(address => address.Address.AddressFamily == addressFamily)
            .Select(address => address.PrefixLength > 0 ? $"{address.Address}/{address.PrefixLength}" : address.Address.ToString())
            .ToList();
    }

    private static string FormatMac(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? "unknown" : string.Join(":", bytes.Select(value => value.ToString("x2")));
    }

    private static string ReadCarrier(string name)
    {
        return ReadSysClassNet(name, "carrier") switch
        {
            "1" => "yes",
            "0" => "no",
            var value => value
        };
    }

    private static string ReadSpeed(string name)
    {
        var speed = ReadSysClassNet(name, "speed");
        return int.TryParse(speed, out _) ? $"{speed} Mb/s" : speed;
    }

    private static string ReadSysClassNet(string name, string fileName)
    {
        var path = Path.Combine("/sys/class/net", name, fileName);
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : "unknown";
        }
        catch
        {
            return "unknown";
        }
    }

    private string GetDefaultRoute(string name)
    {
        var result = commandRunner.Run("ip", ["route", "show", "default", "dev", name]);
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output)
            ? result.Output.Trim().ReplaceLineEndings("; ").TrimEnd(' ', ';')
            : "unknown";
    }

    private string DetectManager(string name)
    {
        var nmcli = commandRunner.Run("nmcli", ["-t", "-f", "DEVICE,STATE", "device", "status"]);
        if (nmcli.ExitCode == 0 && nmcli.Output.Split('\n').Any(line => line.StartsWith($"{name}:", StringComparison.Ordinal)))
        {
            return "NetworkManager";
        }

        var networkctl = commandRunner.Run("networkctl", ["status", name, "--no-pager"]);
        if (networkctl.ExitCode == 0)
        {
            return "systemd-networkd";
        }

        return "unknown";
    }

    private Dictionary<string, string> GetDriverInfo(string name)
    {
        var result = commandRunner.Run("ethtool", ["-i", name]);
        return result.ExitCode == 0 ? ParseColonLines(result.Output) : [];
    }

    private Dictionary<string, string> GetLinkInfo(string name)
    {
        var result = commandRunner.Run("ethtool", [name]);
        return result.ExitCode == 0 ? ParseColonLines(result.Output) : [];
    }

    private static Dictionary<string, string> ParseColonLines(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);
            if (separatorIndex <= 0)
            {
                continue;
            }

            values[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return values;
    }

    private static string GetValue(Dictionary<string, string> values, string key, string fallback = "unknown")
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }
}

internal sealed class CommandRunner
{
    public static bool IsRoot() => string.Equals(Environment.UserName, "root", StringComparison.Ordinal);

    public CommandResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            }.WithArguments(arguments));

            if (process is null)
            {
                return new CommandResult(1, string.Empty, $"Unable to start {fileName}.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                return new CommandResult(124, output, $"Timed out running {fileName}.");
            }

            return new CommandResult(process.ExitCode, output, error);
        }
        catch (Exception ex) when (ex is FileNotFoundException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return new CommandResult(127, string.Empty, ex.Message);
        }
    }
}

internal static class ProcessStartInfoExtensions
{
    public static ProcessStartInfo WithArguments(this ProcessStartInfo startInfo, IReadOnlyList<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}

internal sealed record InterfaceSummary(string Name, string OperState, string Carrier, string IPv4Address, string Speed, string Manager);

internal sealed record InterfaceDetail(
    string Name,
    string MacAddress,
    string Mtu,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> IPv6Addresses,
    string DefaultRoute,
    string Driver,
    string Firmware,
    string BusInfo,
    string Speed,
    string Duplex,
    string Autonegotiation,
    string Manager);

internal sealed record CommandResult(int ExitCode, string Output, string Error);

internal sealed record BackendCommand(string FileName, IReadOnlyList<string> Arguments)
{
    public string DisplayText => string.Join(' ', [FileName, .. Arguments.Select(QuoteIfNeeded)]);

    private static string QuoteIfNeeded(string value) => value.Any(char.IsWhiteSpace) ? $"'{value.Replace("'", "'\\''", StringComparison.Ordinal)}'" : value;
}

internal sealed record CommandPlan(IReadOnlyList<BackendCommand> Commands)
{
    public static CommandPlan Single(string fileName, params string[] arguments) => new([new BackendCommand(fileName, arguments)]);
}
