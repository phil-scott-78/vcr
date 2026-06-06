using Spectre.Console.Cli;
using VcrSharp.Cli.Commands;

var app = new CommandApp<RecordCommand>();

app.Configure(config =>
{
    config.SetApplicationName("vcr");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate a tape file without recording")
        .WithExample(new[] { "validate", "demo.tape" });

    config.AddCommand<ThemesCommand>("themes")
        .WithDescription("List available themes");

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate a directory of tapes to a shared vcr.toml preset (dry run by default)")
        .WithExample(new[] { "migrate", "samples/" })
        .WithExample(new[] { "migrate", "samples/", "--write" });

    config.AddCommand<SnapCommand>("snap")
        .WithDescription("Capture a static SVG screenshot of a command's final output")
        .WithExample(new[] { "snap", "\"echo Hello\"", "-o", "hello.svg" })
        .WithExample(new[] { "snap", "\"ls -la\"", "--theme", "Dracula", "--cols", "100" });

    config.AddCommand<CaptureCommand>("capture")
        .WithDescription("Capture an animated SVG recording of a command's output")
        .WithExample(new[] { "capture", "\"npm install\"", "-o", "install.svg" })
        .WithExample(new[] { "capture", "\"git status\"", "--cols", "80", "--rows", "24" });

    config.AddCommand<NativeSnapCommand>("native-snap")
        .WithDescription("[experimental] Browserless static SVG via in-process PTY (no ttyd/Chromium)")
        .WithExample(new[] { "native-snap", "\"echo Hello\"", "-o", "hello.svg" })
        .WithExample(new[] { "native-snap", "\"dotnet --info\"", "--cols", "100", "--theme", "Dracula" });

    config.AddCommand<NativePlayCommand>("native-play")
        .WithDescription("[experimental] Play a .tape to an animated SVG with no ttyd/Chromium (native ConPTY)")
        .WithExample(new[] { "native-play", "demo.tape", "-o", "demo.svg" });

    config.AddCommand<RecordInteractiveCommand>("record")
        .WithDescription("Interactively record keystrokes in a real shell and generate a .tape file")
        .WithExample(new[] { "record", "demo.tape" })
        .WithExample(new[] { "record", "demo.tape", "--shell", "bash" });
});

return await app.RunAsync(args);