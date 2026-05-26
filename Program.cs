using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Spectre.Console;

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
        Console.WriteLine("  yanm                    Start interactive UI");
        Console.WriteLine("  yanm --help             Print help");
        Console.WriteLine("  yanm --no-ui            Print help and exit");
        Console.WriteLine("  yanm list               Print interface list");
        Console.WriteLine("  yanm show <iface>       Print interface detail");
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
            Console.WriteLine($"{iface.Name}\tstate={iface.OperState}\thardware={iface.HardwareName}\tcarrier={iface.Carrier}\tipv4={iface.IPv4Address}\tipv6={iface.IPv6Address}\tspeed={iface.Speed}\tmanager={iface.Manager}");
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
        Console.WriteLine($"State: {detail.OperState}");
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
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            RunPlain();
            return;
        }

        RunSpectre();
    }

    private void RunSpectre()
    {
        var selectedIndex = 0;

        while (true)
        {
            var interfaces = networkService.GetInterfaces();
            if (selectedIndex >= interfaces.Count)
            {
                selectedIndex = Math.Max(0, interfaces.Count - 1);
            }

            AnsiConsole.Clear();
            AnsiConsole.Write(new Rule("[bold]Network interfaces[/]").RuleStyle("grey"));

            if (interfaces.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No network interfaces detected.[/]");
                AnsiConsole.Prompt(new TextPrompt<string>("Press [bold]Enter[/] to quit.").AllowEmpty());
                return;
            }

            RenderInterfaceTable(interfaces, selectedIndex);
            AnsiConsole.MarkupLine("[grey]Up/Down move, Enter opens, R refreshes, Q quits[/]");

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Q)
            {
                return;
            }

            if (key.Key == ConsoleKey.R)
            {
                continue;
            }

            if (key.Key == ConsoleKey.UpArrow)
            {
                selectedIndex = selectedIndex <= 0 ? interfaces.Count - 1 : selectedIndex - 1;
                continue;
            }

            if (key.Key == ConsoleKey.DownArrow)
            {
                selectedIndex = selectedIndex >= interfaces.Count - 1 ? 0 : selectedIndex + 1;
                continue;
            }

            if (key.Key == ConsoleKey.Enter)
            {
                ShowInterface(interfaces[selectedIndex].Name);
            }
        }
    }

    private void RunPlain()
    {
        while (true)
        {
            var interfaces = networkService.GetInterfaces();
            PrintPlainInterfaceList(interfaces);

            if (interfaces.Count == 0)
            {
                return;
            }

            Console.Write("Select interface number, R to refresh, or Q to quit: ");
            var input = Console.ReadLine()?.Trim();
            if (input is null || string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.Equals(input, "r", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TrySelectNumber(input, interfaces.Count, out var selectedIndex))
            {
                ShowInterface(interfaces[selectedIndex].Name);
            }
        }
    }

    private static void RenderInterfaceTable(IReadOnlyList<InterfaceSummary> interfaces, int selectedIndex)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Expand()
            .AddColumn("#")
            .AddColumn("Name")
            .AddColumn("State")
            .AddColumn("Hardware")
            .AddColumn("Carrier")
            .AddColumn("IPv4")
            .AddColumn("Speed")
            .AddColumn("Manager");

        for (var i = 0; i < interfaces.Count; i++)
        {
            var iface = interfaces[i];
            var selected = i == selectedIndex;
            table.AddRow(
                FormatTableCell((i + 1).ToString(CultureInfo.InvariantCulture), selected),
                FormatTableCell(iface.Name, selected),
                FormatStateCell(iface.OperState, selected),
                FormatTableCell(iface.HardwareName, selected),
                FormatTableCell(iface.Carrier, selected),
                FormatTableCell(iface.IPv4Address, selected),
                FormatTableCell(iface.Speed, selected),
                FormatTableCell(iface.Manager, selected));
        }

        AnsiConsole.Write(table);
    }

    private static string FormatTableCell(string value, bool selected)
    {
        var escaped = Markup.Escape(value);
        return selected ? $"[black on white]{escaped}[/]" : escaped;
    }

    private static string FormatStateCell(string value, bool selected)
    {
        var escaped = Markup.Escape(value);
        return selected ? $"[black on white]{escaped}[/]" : $"[{GetStateStyle(value)}]{escaped}[/]";
    }

    private static void PrintPlainInterfaceList(IReadOnlyList<InterfaceSummary> interfaces)
    {
        Console.WriteLine("Network interfaces");
        Console.WriteLine();

        if (interfaces.Count == 0)
        {
            Console.WriteLine("No network interfaces detected.");
            return;
        }

        for (var i = 0; i < interfaces.Count; i++)
        {
            var iface = interfaces[i];
            Console.WriteLine($"{i + 1}. {iface.Name}\tstate={iface.OperState}\thardware={iface.HardwareName}\tcarrier={iface.Carrier}\tipv4={iface.IPv4Address}\tspeed={iface.Speed}\tmanager={iface.Manager}");
        }

        Console.WriteLine();
    }

    private static bool TrySelectNumber(string value, int count, out int selectedIndex)
    {
        selectedIndex = -1;
        if (!int.TryParse(value, out var selected) || selected < 1 || selected > count)
        {
            return false;
        }

        selectedIndex = selected - 1;
        return true;
    }

    private void ShowInterface(string name)
    {
        while (true)
        {
            var detail = networkService.GetInterfaceDetail(name);
            if (detail is null)
            {
                WriteMessage($"Interface not found: {name}", "red");
                WaitForContinue();
                return;
            }

            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            {
                PrintPlainDetail(detail);
                Console.Write("Select action: ");
                var actionInput = Console.ReadLine()?.Trim();
                if (actionInput is null || !TrySelectNumber(actionInput, ActionItems.Length, out var selectedAction))
                {
                    return;
                }

                if (RunSelectedAction(selectedAction, name))
                {
                    return;
                }

                continue;
            }

            AnsiConsole.Clear();
            RenderInterfaceDetail(detail);
            var action = AnsiConsole.Prompt(
                new SelectionPrompt<ActionMenuItem>()
                    .Title("Select an action")
                    .UseConverter(item => item.Label)
                    .AddChoices(ActionMenuItem.All));

            if (RunSelectedAction(action.Index, name))
            {
                return;
            }
        }
    }

    private static void RenderInterfaceDetail(InterfaceDetail detail)
    {
        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(detail.Name)}[/]").RuleStyle("grey"));
        AnsiConsole.Write(RenderFieldTable("Addresses", [
            ("MAC", detail.MacAddress),
            ("MTU", detail.Mtu),
            ("IPv4", FormatList(detail.IPv4Addresses)),
            ("IPv6", FormatList(detail.IPv6Addresses)),
            ("Default route", detail.DefaultRoute)
        ]));
        AnsiConsole.Write(RenderFieldTable("Link", [
            ("State", detail.OperState),
            ("Driver", detail.Driver),
            ("Firmware", detail.Firmware),
            ("Bus info", detail.BusInfo),
            ("Speed", detail.Speed),
            ("Duplex", detail.Duplex),
            ("Autonegotiation", detail.Autonegotiation),
            ("Manager", detail.Manager)
        ]));
    }

    private static Table RenderFieldTable(string title, IReadOnlyList<(string Label, string Value)> fields)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title(Markup.Escape(title))
            .Expand()
            .AddColumn("Field")
            .AddColumn("Value");

        foreach (var field in fields)
        {
            var value = field.Label == "State"
                ? $"[{GetStateStyle(field.Value)}]{Markup.Escape(field.Value)}[/]"
                : Markup.Escape(field.Value);
            table.AddRow(Markup.Escape(field.Label), value);
        }

        return table;
    }

    private static void PrintPlainDetail(InterfaceDetail detail)
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
        Console.WriteLine();

        for (var i = 0; i < ActionItems.Length; i++)
        {
            Console.WriteLine($"{i + 1}. {ActionItems[i]}");
        }
    }

    private bool RunSelectedAction(int selectedAction, string name)
    {
        switch (selectedAction)
        {
            case 0:
                return false;
            case 1:
                ApplyChangingAction("Bring interface up", CommandPlan.Single("ip", "link", "set", "dev", name, "up"));
                return false;
            case 2:
                ApplyChangingAction("Bring interface down", CommandPlan.Single("ip", "link", "set", "dev", name, "down"));
                return false;
            case 3:
                ApplyChangingAction("Renew DHCP", new CommandPlan([
                    new BackendCommand("dhclient", ["-r", name]),
                    new BackendCommand("dhclient", [name])
                ]));
                return false;
            case 4:
                ApplyChangingAction("Set DHCP", CommandPlan.Single("dhclient", name));
                return false;
            case 5:
                var plan = PromptStaticIPv4Plan(name);
                if (plan is not null)
                {
                    ApplyChangingAction("Set static IPv4", plan);
                }

                return false;
            case 6:
                return true;
            case 7:
                Environment.Exit(0);
                return true;
            default:
                return false;
        }
    }

    private static CommandPlan? PromptStaticIPv4Plan(string name)
    {
        var ipCidr = Prompt("IP/CIDR (example 192.168.1.50/24): ").Trim();
        var gateway = Prompt("Gateway: ").Trim();
        var dnsServers = Prompt("DNS servers (space-separated): ").Trim();

        if (string.IsNullOrWhiteSpace(ipCidr) || string.IsNullOrWhiteSpace(gateway))
        {
            WriteMessage("IP/CIDR and gateway are required.", "red");
            WaitForContinue();
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
        WriteMessage(title, "yellow");
        Console.WriteLine("Backend command(s):");
        foreach (var command in plan.Commands)
        {
            Console.WriteLine($"  {command.DisplayText}");
        }

        if (!string.Equals(Prompt("Apply this change? Type yes to continue: ").Trim(), "yes", StringComparison.Ordinal))
        {
            WriteMessage("No changes made.", "yellow");
            WaitForContinue();
            return;
        }

        if (!CommandRunner.IsRoot())
        {
            WriteMessage("Not running as root. No changes made.", "yellow");
            Console.WriteLine("Run equivalent command(s):");
            foreach (var command in plan.Commands)
            {
                Console.WriteLine($"  sudo {command.DisplayText}");
            }

            WaitForContinue();
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

                WaitForContinue();
                return;
            }
        }

        WriteMessage("Change applied.", "green");
        WaitForContinue();
    }

    private static IReadOnlyList<string> SplitWords(string? value) => string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string FormatList(IReadOnlyList<string> values) => values.Count == 0 ? "unknown" : string.Join(", ", values);

    private static string Prompt(string prompt)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.Write(prompt);
            return Console.ReadLine() ?? string.Empty;
        }

        return AnsiConsole.Prompt(new TextPrompt<string>(Markup.Escape(prompt)).AllowEmpty());
    }

    private static void WaitForContinue()
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
        {
            Console.WriteLine("Press Enter to continue.");
            Console.ReadLine();
            return;
        }

        AnsiConsole.Prompt(new TextPrompt<string>("Press [bold]Enter[/] to continue.").AllowEmpty());
    }

    private static void WriteMessage(string message, string style)
    {
        if (Console.IsOutputRedirected)
        {
            Console.WriteLine(message);
            return;
        }

        AnsiConsole.MarkupLine($"[{style}]{Markup.Escape(message)}[/]");
    }

    private static string GetStateStyle(string state) => state switch
    {
        "Up" => "green",
        "Down" => "red",
        _ => "yellow"
    };

    private static readonly string[] ActionItems = [
        "Refresh",
        "Bring interface up",
        "Bring interface down",
        "Renew DHCP",
        "Set DHCP",
        "Set static IPv4",
        "Back",
        "Quit"
    ];

    private sealed record ActionMenuItem(int Index, string Label)
    {
        public static IReadOnlyList<ActionMenuItem> All { get; } = ActionItems
            .Select((label, index) => new ActionMenuItem(index, label))
            .ToList();
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
                    NormalizeOperState(ReadSysClassNet(name, "operstate")),
                    string.IsNullOrWhiteSpace(interfaceInfo.Description) ? "unknown" : interfaceInfo.Description,
                    ReadCarrier(name),
                    GetIPv4Addresses(name, interfaceInfo).FirstOrDefault() ?? "none",
                    GetIPAddresses(interfaceInfo, AddressFamily.InterNetworkV6).FirstOrDefault() ?? "none",
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
            NormalizeOperState(ReadSysClassNet(name, "operstate")),
            FormatMac(interfaceInfo.GetPhysicalAddress()),
            ReadSysClassNet(name, "mtu"),
            GetIPv4Addresses(name, interfaceInfo),
            GetIPAddresses(interfaceInfo, AddressFamily.InterNetworkV6),
            GetDefaultRoute(name),
            GetValue(driverInfo, "driver"),
            GetValue(driverInfo, "firmware-version"),
            GetValue(driverInfo, "bus-info"),
            FormatSpeed(GetValue(linkInfo, "Speed", ReadSpeed(name))),
            GetValue(linkInfo, "Duplex"),
            GetValue(linkInfo, "Auto-negotiation"),
            DetectManager(name));
    }

    private IReadOnlyList<string> GetIPv4Addresses(string name, NetworkInterface interfaceInfo)
    {
        var source = DetectIPv4Source(name, interfaceInfo);
        return interfaceInfo.GetIPProperties().UnicastAddresses
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => FormatIPWithSource(address, source))
            .ToList();
    }

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

    private static string NormalizeOperState(string value)
    {
        return value switch
        {
            "up" => "Up",
            "down" => "Down",
            "unknown" => "Unknown",
            _ => "Unknown"
        };
    }

    private static string ReadSpeed(string name)
    {
        var speed = ReadSysClassNet(name, "speed");
        return FormatSpeed(speed);
    }

    private static string FormatSpeed(string value)
    {
        var normalized = value.Trim();
        var digits = new string(normalized.TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out var megabits))
        {
            return normalized;
        }

        if (megabits > 100)
        {
            var gigabits = megabits / 1000m;
            return $"{gigabits.ToString("0.##", CultureInfo.InvariantCulture)} Gb/s";
        }

        return $"{megabits} Mb/s";
    }

    private string DetectIPv4Source(string name, NetworkInterface interfaceInfo)
    {
        var addresses = interfaceInfo.GetIPProperties().UnicastAddresses
            .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
            .Select(address => address.Address)
            .ToList();

        if (addresses.Count == 0)
        {
            return "Static";
        }

        if (addresses.Any(IsApipa))
        {
            return "APIPA";
        }

        return HasActiveDhcpLease(name) ? "DHCP" : "Static";
    }

    private static string FormatIPWithSource(UnicastIPAddressInformation address, string source)
    {
        var display = address.PrefixLength > 0 ? $"{address.Address}/{address.PrefixLength}" : address.Address.ToString();
        var actualSource = IsApipa(address.Address) ? "APIPA" : source;
        return $"{display} ({actualSource})";
    }

    private bool HasActiveDhcpLease(string name)
    {
        var ifIndex = ReadSysClassNet(name, "ifindex");
        if (ifIndex != "unknown" && File.Exists(Path.Combine("/run/systemd/netif/leases", ifIndex)))
        {
            return true;
        }

        var nmcli = commandRunner.Run("nmcli", ["-g", "IP4.METHOD", "device", "show", name]);
        if (nmcli.ExitCode == 0 && nmcli.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => string.Equals(line, "auto", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }

    private static bool IsApipa(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length >= 2 && bytes[0] == 169 && bytes[1] == 254;
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

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                process.Kill(entireProcessTree: true);
                return new CommandResult(124, string.Empty, $"Timed out running {fileName}.");
            }

            return new CommandResult(process.ExitCode, outputTask.GetAwaiter().GetResult(), errorTask.GetAwaiter().GetResult());
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

internal sealed record InterfaceSummary(string Name, string OperState, string HardwareName, string Carrier, string IPv4Address, string IPv6Address, string Speed, string Manager);

internal sealed record InterfaceDetail(
    string Name,
    string OperState,
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
