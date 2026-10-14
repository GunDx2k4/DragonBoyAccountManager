using Spectre.Console;
using DragonBoyAccountManager.Core.Game;

AnsiConsole.Write(new Rule("[bold cyan]DBAM[/]").Centered());
AnsiConsole.WriteLine();

string gamePath = AnsiConsole.Ask<string>("Game path:");
ResolvedGamePaths paths;

try
{
    paths = GameLauncher.Validate(gamePath);
}
catch (ArgumentException ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return;
}

PrintKV("Data", paths.DataPath);
PrintKV("Managed", paths.ManagedPath);
AnsiConsole.WriteLine();

var launcher = new GameLauncher();
GameInstance instance;

try
{
    instance = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Launching...", async _ =>
        {
            return await launcher.LaunchAsync(new LaunchOptions
            {
                GamePath = gamePath,
                LogPrefix = "DBAM"
            });
        });
}
catch (Exception ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return;
}

using var controller = new GameController(instance);

AnsiConsole.MarkupLine($"[green]Attached[/] PID={instance.ProcessId} RootDomain=0x{instance.MonoContext.RootDomain:X}");
AnsiConsole.WriteLine();

instance.Exited += (_, _) => AnsiConsole.MarkupLine("[yellow]Game exited.[/]");

while (instance.IsRunning)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Action:")
            .HighlightStyle(Style.Parse("cyan"))
            .AddChoices(
                "Status",
                "TimeScale (Speed)",
                "Auto Login",
                "Read GameMidlet",
                "Read MyChar",
                controller.IsRunning ? "Stop Loop" : "Start Loop (1s)",
                "Kill",
                "Exit"));

    switch (choice)
    {
        case "Status":
            PrintStatus(instance, controller);
            break;

        case "TimeScale (Speed)":
            float current = instance.GetTimeScale();
            float newSpeed = AnsiConsole.Ask<float>($"Current TimeScale: [yellow]{current:0.0}x[/]. Enter new value:");
            instance.SetTimeScale(newSpeed);
            AnsiConsole.MarkupLine($"[green]TimeScale updated to {instance.GetTimeScale():0.0}x[/]");
            break;

        case "Auto Login":
            await RunAutoLogin(controller);
            break;

        case "Read GameMidlet":
            PrintKV("VERSION", instance.Midlet.Version);
            PrintKV("intVERSION", instance.Midlet.IntVersion.ToString());
            PrintKV("IP", instance.Midlet.IP);
            PrintKV("PORT", instance.Midlet.Port.ToString());
            break;

        case "Read MyChar":
            if (!instance.MyChar.IsInGame)
            {
                AnsiConsole.MarkupLine("[yellow]Character not in game (myChar pointer is null).[/]");
            }
            else
            {
                PrintKV("Name", instance.MyChar.Name);
                PrintKV("Power", instance.MyChar.Power.ToString("N0"));
            }
            break;

        case "Start Loop (1s)":
            controller.Ticked += OnControllerTicked;
            controller.Start(TimeSpan.FromSeconds(1));
            AnsiConsole.MarkupLine("[green]Loop started (ticking every 1s).[/]");
            break;

        case "Stop Loop":
            controller.Ticked -= OnControllerTicked;
            controller.Stop();
            AnsiConsole.MarkupLine("[yellow]Loop stopped.[/]");
            break;

        case "Kill":
            instance.Kill();
            break;

        case "Exit":
            goto done;
    }
}

done:
controller.Stop();
instance.Dispose();

static void PrintKV(string key, string value)
    => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(key)}:[/] {Markup.Escape(value)}");

static void PrintStatus(GameInstance inst, GameController ctrl)
{
    PrintKV("PID", inst.ProcessId.ToString());
    PrintKV("Running", inst.IsRunning.ToString());
    PrintKV("RootDomain", $"0x{inst.MonoContext.RootDomain:X}");
    PrintKV("In Game", inst.MyChar.IsInGame ? "Yes" : "No");
    PrintKV("Loop Running", ctrl.IsRunning ? "Yes" : "No");
    PrintKV("Canvas Alive", inst.Canvas.IsAlive ? "Yes" : "No");
    PrintKV("Session Connected", inst.Session.IsConnected ? "Yes" : "No");
}

static void OnControllerTicked(GameInstance inst)
{
    if (inst.MyChar.IsInGame)
    {
        AnsiConsole.MarkupLine($"[dim][[Tick]][/] Char: [cyan]{Markup.Escape(inst.MyChar.Name)}[/] | Power: [yellow]{inst.MyChar.Power:N0}[/]");
    }
}

static async Task RunAutoLogin(GameController ctrl)
{
    string user = AnsiConsole.Ask<string>("Username:");
    string pass = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
    int serverId = AnsiConsole.Prompt(
        new TextPrompt<int>("Server ID:")
            .DefaultValue(0));

    bool ok = false;
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Logging in...", async ctx =>
        {
            var progress = new Progress<string>(msg =>
            {
                ctx.Status(Markup.Escape(msg));
                AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] {Markup.Escape(msg)}");
            });
            ok = await ctrl.ExecuteAsync(new AutoLoginAction(user, pass, serverId), progress);
        });

    if (ok)
    {
        AnsiConsole.MarkupLine($"[green]Login successful![/] Character: [cyan]{Markup.Escape(ctrl.Instance.MyChar.Name)}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Login failed (timed out or game was closed).[/]");
    }
}