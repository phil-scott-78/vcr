using Spectre.Console;
using VcrSharp.Cli.Helpers;

namespace VcrSharp.Cli.Commands;

public class Logo
{
    public static void WriteLogo()
    {
        var blue = TerminalPalette.Query(ConsoleColor.Red);
        var aqua = TerminalPalette.Query(ConsoleColor.Yellow);

        var font = FigletFont.Parse(FigletFonts.Ascii3d);
        var colors = new[] { ConvertColor(blue, Color.Red), ConvertColor(aqua, Color.Yellow)};

        var figlet = new FigletText(font, "VCR#");
        var figletWithGradient = new Gradient(figlet, colors, GradientDirection.BottomLeftToTopRight);
        var padded = new Padder(figletWithGradient, new Padding(1));
        AnsiConsole.Write(padded);
        AnsiConsole.WriteLine();
    }

    private static Color ConvertColor((byte R, byte G, byte B)? blue, Color defaultColor)
    {
        return !blue.HasValue
            ? defaultColor
            : new Color(blue.Value.R, blue.Value.G, blue.Value.B);
    }
}