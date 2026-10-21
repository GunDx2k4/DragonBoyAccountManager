using Spectre.Console;
using DragonBoyAccountManager.Core.Sessions;
using DragonBoyAccountManager.Core.Sessions.Validation;
using DragonBoyAccountManager.Core.Automation.Behaviors.Combat;
using DragonBoyAccountManager.Core.Automation.Behaviors.Lifecycle;

AnsiConsole.Write(new Rule("[bold cyan]DBAM[/]").Centered());
AnsiConsole.WriteLine();

string gamePath = AnsiConsole.Ask<string>("Game path:");
ResolvedGamePaths paths;

try
{
    paths = SessionLauncher.Validate(gamePath);
}
catch (ArgumentException ex)
{
    AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
    return;
}

PrintKV("Data", paths.DataPath);
PrintKV("Managed", paths.ManagedPath);
AnsiConsole.WriteLine();

var launcher = new SessionLauncher();
GameSession session;

try
{
    session = await AnsiConsole.Status()
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

AnsiConsole.MarkupLine($"[green]Attached[/] PID={session.ProcessId} RootDomain=0x{session.Mono.RootDomain:X}");
AnsiConsole.WriteLine();

session.Exited += (_, _) => AnsiConsole.MarkupLine("[yellow]Game exited.[/]");

CancellationTokenSource? loopCts = null;
Task? loopTask = null;

while (session.IsRunning)
{
    bool isLoopRunning = loopTask != null && !loopTask.IsCompleted;
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Action:")
            .HighlightStyle(Style.Parse("cyan"))
            .AddChoices(
                "Status",
                "Tick Once",
                "Auto Login",
                "Read Player",
                isLoopRunning ? "Stop Loop" : "Start Loop (1s)",
                "Kill",
                "Exit"));

    switch (choice)
    {
        case "Status":
            PrintStatus(session);
            break;

        case "Tick Once":
            session.Tick();
            PrintPlayer(session);
            break;

        case "Auto Login":
            await RunAutoLogin(session);
            break;

        case "Read Player":
            session.Tick();
            PrintPlayer(session);
            break;

        case "Start Loop (1s)":
            loopCts = new CancellationTokenSource();
            var token = loopCts.Token;
            loopTask = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested && session.IsRunning)
                {
                    session.Tick();
                    if (session.CurrentPlayer.IsInGame)
                    {
                        AnsiConsole.MarkupLine($"[dim][[Tick]][/] Char: [cyan]{Markup.Escape(session.CurrentPlayer.Name)}[/] | Power: [yellow]{session.CurrentPlayer.Power:N0}[/]");
                    }
                    await Task.Delay(1000, token);
                }
            }, token);
            AnsiConsole.MarkupLine("[green]Loop started (ticking every 1s).[/]");
            break;

        case "Stop Loop":
            loopCts?.Cancel();
            loopCts?.Dispose();
            loopCts = null;
            loopTask = null;
            AnsiConsole.MarkupLine("[yellow]Loop stopped.[/]");
            break;

        case "Kill":
            session.Kill();
            break;

        case "Exit":
            goto done;
    }
}

done:
loopCts?.Cancel();
session.Dispose();

static void PrintKV(string key, string value)
    => AnsiConsole.MarkupLine($"[grey]{Markup.Escape(key)}:[/] {Markup.Escape(value)}");

static void PrintStatus(GameSession sess)
{
    PrintKV("PID", sess.ProcessId.ToString());
    PrintKV("Running", sess.IsRunning.ToString());
    PrintKV("RootDomain", $"0x{sess.Mono.RootDomain:X}");
    PrintKV("In Game", sess.CurrentPlayer.IsInGame ? "Yes" : "No");
}

static void PrintPlayer(GameSession sess)
{
    if (!sess.CurrentPlayer.IsInGame)
    {
        AnsiConsole.MarkupLine("[yellow]Character not in game.[/]");
    }
    else
    {
        PrintKV("Name", sess.CurrentPlayer.Name);
        PrintKV("HP", $"{sess.CurrentPlayer.Hp}/{sess.CurrentPlayer.MaxHp}");
        PrintKV("MP", $"{sess.CurrentPlayer.Mp}/{sess.CurrentPlayer.MaxMp}");
        PrintKV("Power", sess.CurrentPlayer.Power.ToString("N0"));
        PrintKV("Position", $"({sess.CurrentPlayer.X}, {sess.CurrentPlayer.Y})");
    }
}

static async Task RunAutoLogin(GameSession sess)
{
    string user = AnsiConsole.Ask<string>("Username:");
    string pass = AnsiConsole.Prompt(new TextPrompt<string>("Password:").Secret());
    int serverId = AnsiConsole.Prompt(
        new TextPrompt<int>("Server ID:")
            .DefaultValue(0));

    var options = new AutoLoginOptions
    {
        Username = user,
        Password = pass,
        ServerId = serverId
    };

    var behavior = new AutoLoginBehavior(options, sess.Mono);
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Logging in...", async ctx =>
        {
            var progress = new Progress<string>(msg =>
            {
                ctx.Status(Markup.Escape(msg));
                AnsiConsole.MarkupLine($"[grey][[{DateTime.Now:HH:mm:ss}]][/] {Markup.Escape(msg)}");
            });

            await behavior.StartAsync(
                () => sess.PlayerScraper.ScrapePlayer(),
                () => sess.WorldScraper.ScrapeWorld(),
                () => sess.MapScraper.ScrapeMap(),
                sess.Dispatcher,
                progress);
        });

    sess.Tick();
    if (sess.CurrentPlayer.IsInGame)
    {
        AnsiConsole.MarkupLine($"[green]Login successful![/] Character: [cyan]{Markup.Escape(sess.CurrentPlayer.Name)}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine("[red]Login failed or timed out.[/]");
    }
}
