using ShellProgressBar;

namespace PotatoDBMapper.Utils;

public static class ProgressBar
{
    public static ProgressBarOptions Options => new()
    {
        ForegroundColor = ConsoleColor.Yellow,
        BackgroundColor = ConsoleColor.DarkYellow,
        ProgressCharacter = '─',
        ProgressBarOnBottom = true
    };
}