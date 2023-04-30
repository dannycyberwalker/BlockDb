namespace BlockDb;

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