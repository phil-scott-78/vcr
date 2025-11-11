using Spectre.Console;
using Spectre.Console.Cli;
using VcrSharp.Cli.Helpers;
using VcrSharp.Core.Logging;
using VcrSharp.Core.Parsing;
using VcrSharp.Core.Parsing.Ast;
using VcrSharp.Core.Session;
using VcrSharp.Infrastructure.Playwright;
using VcrSharp.Infrastructure.Processes;
using VcrSharp.Infrastructure.Session;

namespace VcrSharp.Cli.Commands;

/// <summary>
/// Command to record a tape file.
/// </summary>
public class RecordCommand : AsyncCommand<RecordCommand.Settings>
{
    /// <summary>
    /// Settings for the record command.
    /// </summary>
    public class Settings : CommandSettings
    {
        /// <summary>
        /// Gets or sets the tape file path.
        /// </summary>
        [CommandArgument(0, "<tape-file>")]
        public string TapeFile { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to enable verbose logging.
        /// </summary>
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; set; }

        /// <summary>
        /// Gets or sets SET command overrides from command-line.
        /// These override any SET commands in the tape file.
        /// Format: Key=Value (e.g., --set FontSize=24 --set Theme=Dracula)
        /// </summary>
        [CommandOption("--set <KEY=VALUE>")]
        [PairDeconstructor(typeof(SetCommandDeconstructor))]
        public ILookup<string, string>? SetOverrides { get; set; }

        /// <summary>
        /// Gets or sets additional output files from command-line.
        /// These are appended to any Output commands in the tape file.
        /// </summary>
        [CommandOption("-o|--output <FILE>")]
        public string[]? OutputFiles { get; set; }
    }


    /// <summary>
    /// Executes the record command asynchronously.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Configure logging based on verbose flag
        VcrLogger.Configure(settings.Verbose);

        try
        {
            WriteLogo();

            // Validate dependencies
            var missing = DependencyValidator.ValidateDependencies();
            if (missing.Count > 0)
            {
                AnsiConsole.MarkupLine("[bold red]Error:[/] Missing required dependencies:");
                foreach (var dep in missing)
                {
                    AnsiConsole.MarkupLineInterpolated($"  [red]✗[/] {dep}");
                }
                return 1;
            }

            // Validate tape file exists
            if (!File.Exists(settings.TapeFile))
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] Tape file not found: {settings.TapeFile}");
                return 1;
            }

            try
            {
                // Parse tape file
                AnsiConsole.MarkupLineInterpolated($"[dim]Parsing tape file:[/] {settings.TapeFile}");
                var parser = new TapeParser();
                var commands = await parser.ParseFileAsync(settings.TapeFile);

                // Apply CLI overrides (--set and --output parameters)
                commands = ApplyCliOverrides(commands, settings);

                // Extract session options from Set commands
                var options = SessionOptions.FromCommands(commands);

                AnsiConsole.MarkupLineInterpolated($"[green]✓[/] Parsed {commands.Count} commands");
                AnsiConsole.WriteLine();

                // Display settings summary
                DisplaySettingsSummary(options);
                AnsiConsole.WriteLine();

                // Validate Require commands from tape file
                var requireCommands = commands.OfType<RequireCommand>().ToList();
                if (requireCommands.Count > 0)
                {
                    var missingPrograms = requireCommands
                        .Where(r => !ProcessHelper.IsProgramAvailable(r.ProgramName))
                        .Select(r => r.ProgramName)
                        .ToList();

                    if (missingPrograms.Count > 0)
                    {
                        AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] Required programs not found: {string.Join(", ", missingPrograms)}");
                        return 1;
                    }
                }

                // Ensure Playwright browser is installed (will auto-install if needed with its own UI)
                PlaywrightBrowser.EnsureBrowsersInstalled();

                // Record the tape with progress reporting
                RecordingResult? result = null;
                await AnsiConsole.Status()
                    .StartAsync("Initializing...", async ctx =>
                    {
                        var progress = new Progress<string>(status => ctx.Status(status));

                        await using var session = new VcrSession(options);
                        result = await session.RecordAsync(commands, cancellationToken, progress);
                    });

                // Display results
                AnsiConsole.MarkupLine("[green]✓[/] Recording complete");
                AnsiConsole.MarkupLineInterpolated($"[dim]Frames captured:[/] {result!.FrameCount}");
                AnsiConsole.MarkupLineInterpolated($"[dim]Duration:[/] {result.Duration.TotalSeconds:F2}s");

                if (result.OutputFiles.Count > 0)
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]✓[/] Videos rendered:");
                    foreach (var outputFile in result.OutputFiles)
                    {
                        var fileName = Path.GetFileName(outputFile);
                        var fileSize = new FileInfo(outputFile).Length / 1024.0; // KB
                        AnsiConsole.MarkupLineInterpolated($"  [dim]•[/] {fileName} ({fileSize:F1} KB)");
                    }
                }

                return 0;
            }
            catch (TapeParseException ex)
            {
                ErrorReporter.DisplayParseError(ex, settings.TapeFile);
                return 1;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLineInterpolated($"[bold red]Error:[/] {ex.Message}");
                if (ex.InnerException != null)
                {
                    AnsiConsole.MarkupLineInterpolated($"[dim]{ex.InnerException.Message}[/]");
                }
                return 1;
            }
        }
        finally
        {
            VcrLogger.Close();
        }
    }

    /// <summary>
    /// Applies command-line overrides to the parsed tape file commands.
    /// CLI SET commands override tape file SET commands.
    /// CLI Output commands are appended to tape file Output commands.
    /// </summary>
    private static List<VcrSharp.Core.Parsing.Ast.ICommand> ApplyCliOverrides(List<VcrSharp.Core.Parsing.Ast.ICommand> tapeCommands, Settings settings)
    {
        var result = new List<VcrSharp.Core.Parsing.Ast.ICommand>(tapeCommands);

        // Apply SET overrides: remove tape file SET commands that match CLI keys
        if (settings.SetOverrides != null && settings.SetOverrides.Any())
        {
            var cliSetKeys = settings.SetOverrides.Select(g => g.Key.ToLowerInvariant()).ToHashSet();

            // Remove SET commands from tape that are overridden by CLI
            result.RemoveAll(cmd =>
                cmd is SetCommand setCmd &&
                cliSetKeys.Contains(setCmd.SettingName.ToLowerInvariant()));

            // Add CLI SET commands at the beginning (before any action commands)
            var cliSetCommands = settings.SetOverrides
                .Select(group => new SetCommand(group.Key, group.First(), lineNumber: 0))
                .ToList<VcrSharp.Core.Parsing.Ast.ICommand>();

            // Find the insertion point (after existing SET/Output commands, before action commands)
            var insertIndex = 0;
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i] is SetCommand or OutputCommand)
                {
                    insertIndex = i + 1;
                }
                else
                {
                    break;
                }
            }

            result.InsertRange(insertIndex, cliSetCommands);
        }

        // Append CLI Output commands
        if (settings.OutputFiles is { Length: > 0 })
        {
            var cliOutputCommands = settings.OutputFiles
                .Select(file => new OutputCommand(file))
                .ToList<VcrSharp.Core.Parsing.Ast.ICommand>();

            // Find the insertion point (after all SET/Output commands, before action commands)
            var insertIndex = 0;
            for (var i = 0; i < result.Count; i++)
            {
                if (result[i] is SetCommand or OutputCommand)
                {
                    insertIndex = i + 1;
                }
                else
                {
                    break;
                }
            }

            result.InsertRange(insertIndex, cliOutputCommands);
        }

        return result;
    }

    private static void WriteLogo()
    {
        var font = FigletFont.Parse(FigletFonts.Ascii3d);
        var colors = new[] { Color.Blue, Color.Aqua };
        var figlet = new FigletText(font, "VCR#");
        var figletWithGradient = new Gradient(figlet, colors, GradientDirection.BottomLeftToTopRight);
        var padded = new Padder(figletWithGradient, new Padding(1));
        AnsiConsole.Write(padded);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a formatted summary of the recording settings.
    /// </summary>
    private static void DisplaySettingsSummary(SessionOptions options)
    {
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.BorderColor(Color.Grey);
        table.AddColumn(new TableColumn("[dim]Setting[/]").NoWrap());
        table.AddColumn(new TableColumn("[dim]Value[/]"));

        // Dimensions
        var dimensionDisplay = options.Cols > 0 && options.Rows > 0
            ? $"{options.Cols}x{options.Rows} (cols × rows)"
            : $"{options.Width}x{options.Height} (pixels)";
        table.AddRow("Dimensions", dimensionDisplay);

        // Font
        table.AddRow("Font", $"{options.FontFamily} {options.FontSize}px");

        // Video
        table.AddRow("Framerate", $"{options.Framerate} fps");

        // Theme
        table.AddRow("Theme", options.Theme.Name);

        // Shell
        table.AddRow("Shell", options.Shell);

        // Output files
        var outputs = string.Join(", ", options.OutputFiles.Select(Path.GetFileName));
        table.AddRow("Output", outputs);

        AnsiConsole.Write(table);
    }
}