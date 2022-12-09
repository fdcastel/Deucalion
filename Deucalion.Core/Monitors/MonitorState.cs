namespace Deucalion.Monitors;

public class MonitorState
{
    protected MonitorState() { }

    public static MonitorState Unknown { get; } = new();
    public static MonitorState Down { get; } = new();
    public static MonitorState Up { get; } = new();

    public static MonitorState FromBool(bool value) => value ? Up : Down;
    public static bool? ToBool(MonitorState value)
    {
        if (value == null || value == Unknown)
            return null;

        return value == Up;
    }

    public override string ToString() =>
        ToBool(this) switch
        {
            true => "Up",
            false => "Down",
            _ => "Unknown"
        };
}
