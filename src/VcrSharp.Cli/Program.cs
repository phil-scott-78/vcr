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
});

return await app.RunAsync(args);