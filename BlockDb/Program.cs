// guid = 2522688b-7eba-4709-99c1-6391b197c4ca
// format file name = [blockdb];2023-02-05T18:00:10.1151559+00:00
// format in db = [Index key(Guid)][Data length(long)][Data (string)]

using System.Text;
using System.Text.Json;

var user1 = new MockUser("Vova", 23);
var user2 = new MockUser("Petya", 35);
var user3 = new MockUser("Lida", 46);
var client = BlockDbClient.Connect("C:\\Users\\DannyCW\\Desktop\\BlockDbStorage");
client.Put(Guid.Parse("1522688b-7eba-4709-99c1-6391b197c4ca"), JsonSerializer.Serialize(user1));
client.Put(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"), JsonSerializer.Serialize(user2));
client.Put(Guid.Parse("3522688b-7eba-4709-99c1-6391b197c4ca"), JsonSerializer.Serialize(user3));
client.Get(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"));


class BlockDbClient
{
    private const long GuidLengthInBytes = 36;
    private const long SegmentSizeInBytes = 8 * 1024;
    private List<Segment> _segments;
    private Segment _workSegment;
    private string StoragePath { get; }

    private BlockDbClient(string storagePath, List<Segment> segments)
    {
        StoragePath = storagePath;
        _segments = segments;
        _workSegment = segments.LastOrDefault();
    }

    public static BlockDbClient Connect(string storagePath)
    {
        var segments = LoadSegments(storagePath).ToList();
        var client = new BlockDbClient(storagePath, segments);
        
        return client;
    }

    public void Put(Guid guid, string data)
    {
        SetUpNewWorkSegmentIfRequired();
        
        var guidChars = guid.ToString().ToCharArray();
        var dataChars = data.ToCharArray();
        var dataCharsLength = dataChars.LongLength;
        var totalCharsLength = guidChars.LongLength + sizeof(long) + dataChars.LongLength;

        if (totalCharsLength >= SegmentSizeInBytes)
            throw new ArgumentException($"Segment size smaller then data to write.[{totalCharsLength} >= {SegmentSizeInBytes}]");
        
        if (WillBeOverflow(totalCharsLength))
            _workSegment = Segment.Create(StoragePath);

        using var fs = new FileStream(Path.Combine(StoragePath, _workSegment.FileName), FileMode.Append);
        using var w = new BinaryWriter(fs);
        
        w.Write(guidChars);
        w.Write(dataCharsLength);
        w.Write(dataChars);
    }

    public string? Get(Guid guid)
    {
        foreach (var segment in _segments.OrderByDescending(u => u.CreationDate))
        {
            using var fs = new FileStream(Path.Combine(StoragePath, segment.FileName), FileMode.Open);
            using var w = new BinaryReader(fs);
            var keyChars = guid.ToString().ToCharArray();
            long? lastFoundAfterKeyPosition = null;


            // full read segment and find last data with key
            while (fs.Position < fs.Length)
            {
                var readKeyChars = w.ReadChars(keyChars.Length);

                var isItRightKey = true;
                for (var i = 0; i < readKeyChars.Length; i++)
                {
                    if(keyChars[i] == readKeyChars[i])
                        continue;

                    isItRightKey = false;
                    break;
                }

                if (isItRightKey)
                    lastFoundAfterKeyPosition = fs.Position;

                var currentDataLength = w.ReadInt64();
                fs.Position += currentDataLength;
            }
            
            if(lastFoundAfterKeyPosition == null)
                continue;

            fs.Position = lastFoundAfterKeyPosition.Value;
            var matchedDataLength = w.ReadInt64();
            var data = w.ReadBytes((int)matchedDataLength);

            return Encoding.UTF8.GetString(data);
        }
        
        
        return null;
    }

    private bool WillBeOverflow(long writeSize)
    {
        var size = new FileInfo(Path.Combine(StoragePath, _workSegment.FileName)).Length;

        return size + writeSize > SegmentSizeInBytes;
    }

    private void SetUpNewWorkSegmentIfRequired()
    {
        if(_workSegment != null)
            return;

        var segment = Segment.Create(StoragePath);
        _workSegment = segment;
        _segments.Add(segment);
    }
    
    private static IEnumerable<Segment> LoadSegments(string storagePath)
    {
        var filePaths = Directory.GetFiles(storagePath);
        var fileNames = new List<string>(filePaths.Length);
        fileNames.AddRange(filePaths.Select(Path.GetFileName));

        return fileNames.Where(u => u.StartsWith(Segment.FilePrefix))
            .Select(u => new Segment(DateTimeOffset.Parse(u
                .Substring(10, 33)
                .Replace(';', ':'))))
            .OrderBy(u => u.CreationDate)
            .ToList();
    }
}

class Segment
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

public record MockUser(string Name, int Age);