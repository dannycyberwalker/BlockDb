using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BlockDb.Models;

namespace BlockDb;

public class BlockDbClient
{
    private const long GuidLengthInBytes = 36;
    private const long SegmentSizeInBytes = 8 * 1024;
    private List<Segment> _segments;
    private Segment _workSegment;
    private string StoragePath { get; }
    private ConcurrentQueue<ICommand> OnWorkCommandsQueue { get; } = new();
    private ConcurrentQueue<CompletedCommand> CompletedCommandsQueue { get; } = new();
    private Thread Worker;

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
        client.Worker = new Thread(() => client.ProcessQueue());
        client.Worker.Start();
        return client;
    }

    private async void ProcessQueue()
    {
        while (true)
        {
            if (OnWorkCommandsQueue.IsEmpty)
            {
                Thread.Sleep(100);
                continue;
            }
            
            var tryDequeueResult = OnWorkCommandsQueue.TryDequeue(out var command);
            if (!tryDequeueResult)
            {
                Thread.Sleep(10);
                continue;
            }

            await (command switch
            {
                WriteCommand cmd => ProcessPut(cmd),
                ReadCommand cmd => ProcessGet(cmd),
                _ => throw new ArgumentException($"Not supported type of {command?.GetType().ToString() ?? "null"}"),
            });
            Console.WriteLine($"{command.Key.ToString()} was processed.");
            
            
            Thread.Sleep(10);
        }
    }

    public void Put<T>(Guid guid, T data) where T: class
    {
        //TODO: handle exception
        OnWorkCommandsQueue.Enqueue(new WriteCommand(guid, JsonSerializer.Serialize(data)));
        Console.WriteLine($"{guid.ToString()} was enqueue.");
        
        //TODO: wait for writing, because we need make sure about successful operation handling
    }

    private async Task ProcessPut(WriteCommand command)
    {
        SetUpNewWorkSegmentIfRequired();
        
        var guidChars = command.Key.ToString().ToCharArray();
        var dataChars = command.Value.ToCharArray();
        var dataCharsLength = dataChars.LongLength;
        var totalCharsLength = guidChars.LongLength + sizeof(long) + dataChars.LongLength;

        if (totalCharsLength >= SegmentSizeInBytes)
            throw new ArgumentException($"Segment size smaller then data to write.[{totalCharsLength} >= {SegmentSizeInBytes}]");
        
        if (WillBeOverflow(totalCharsLength))
            _workSegment = Segment.Create(StoragePath);

        await using var fs = new FileStream(Path.Combine(StoragePath, _workSegment.FileName), FileMode.Append);
        await using var w = new BinaryWriter(fs);
        
        w.Write(guidChars);
        w.Write(dataCharsLength);
        w.Write(dataChars);
    }

    public async Task<T?> Get<T>(Guid guid) where T: class
    {
        OnWorkCommandsQueue.Enqueue(new ReadCommand(guid));

        CompletedCommand? completedCommand = null;
        while (completedCommand == null)
        {
            var wasDequeued = CompletedCommandsQueue.TryDequeue(out completedCommand);
            if (wasDequeued) 
                break;
            
            await Task.Delay(10);
            completedCommand = null;
        }
        
        var result = completedCommand.Status switch
        {
            CommandStatus.Successful => JsonSerializer.Deserialize<T>(completedCommand.Value),
            CommandStatus.NotFound => null,
            _ => throw new ArgumentException($"Not supported type of {completedCommand.Status}"),
        };

        return result;
    }

    private async Task ProcessGet(ReadCommand command)
    {
        foreach (var segment in _segments.OrderByDescending(u => u.CreationDate))
        {
            await using var fs = new FileStream(Path.Combine(StoragePath, segment.FileName), FileMode.Open);
            using var reader = new BinaryReader(fs);
            var keyChars = command.Key.ToString().ToCharArray();
            long? lastFoundAfterKeyPosition = null;
            
            // full read segment and find last data with key
            // NOTE: we can optimize this algorithm if metadata would be after main data for reading from end.
            while (fs.Position < fs.Length)
            {
                var readKeyChars = reader.ReadChars(keyChars.Length);

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

                var currentDataLength = reader.ReadInt64();
                fs.Position += currentDataLength;
            }
            
            if(lastFoundAfterKeyPosition == null)
                continue;

            fs.Position = lastFoundAfterKeyPosition.Value;
            var matchedDataLength = reader.ReadInt64();
            var data = reader.ReadBytes((int)matchedDataLength);
            var dataInString = Encoding.UTF8.GetString(data);
            var successfulCompleted = new CompletedCommand(command.Key, dataInString, CommandStatus.Successful); 

            CompletedCommandsQueue.Enqueue(successfulCompleted);
            return;
        }
        
        var notFoundCompleted = new CompletedCommand(command.Key, string.Empty, CommandStatus.NotFound); 
        CompletedCommandsQueue.Enqueue(notFoundCompleted);
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