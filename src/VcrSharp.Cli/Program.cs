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

    config.AddCommand<SnapCommand>("snap")
        .WithDescription("Capture a static SVG screenshot of a command's final output")
        .WithExample(new[] { "snap", "\"echo Hello\"", "-o", "hello.svg" })
        .WithExample(new[] { "snap", "\"ls -la\"", "--theme", "Dracula", "--cols", "100" });

    config.AddCommand<CaptureCommand>("capture")
        .WithDescription("Capture an animated SVG recording of a command's output")
        .WithExample(new[] { "capture", "\"npm install\"", "-o", "install.svg" })
        .WithExample(new[] { "capture", "\"git status\"", "--cols", "80", "--rows", "24" });
});

return await app.RunAsync(args);