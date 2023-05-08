namespace BlockDb.Models;

internal class Segment
{
    public const string FilePrefix = "[blockdb]";
    public DateTimeOffset CreationDate { get; }

    public Segment(DateTimeOffset creationDate)
    {
        CreationDate = creationDate;
    }

    public static Segment Create(string storagePath)
    {
        var date = DateTimeOffset.UtcNow;
        File.Create(Path.Combine(storagePath, $"{FilePrefix};{date.ToString("O").Replace(':', ';')}.bin")).Dispose();
        return new Segment(date);
    }

    public string FileName => $"{FilePrefix};{CreationDate.ToString("O").Replace(':', ';')}.bin";
}

internal interface ICommand
{
    Guid Key { get; }
}
internal record WriteCommand(Guid Key, string Value) : ICommand;
internal record ReadCommand(Guid Key) : ICommand;
internal record CompletedCommand(Guid Key, string Value, CommandStatus Status): ICommand;

internal enum CommandStatus
{
    Failed = 0,
    Successful = 1,
    NotFound = 2,
} 