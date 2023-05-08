namespace BlockDb;

public record MockUser(string Name, int Age);

class Program
{
    public ReaderWriterLock FileAccessManager = new();
    private static readonly BlockDbClient Client;

    static Program()
    {
        Client = BlockDbClient.Connect("C:\\Users\\DannyCW\\Desktop\\BlockDbStorage");
    }
    
    public static async Task Main()
    {
        // guid = 2522688b-7eba-4709-99c1-6391b197c4ca

        var user1 = new MockUser("Vova", 23);
        var user2 = new MockUser("Petya", 35);
        var user3 = new MockUser("Lida", 46);
        
        Client.Put(Guid.Parse("1522688b-7eba-4709-99c1-6391b197c4ca"), user1);
        Client.Put(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"), user2);
        Client.Put(Guid.Parse("3522688b-7eba-4709-99c1-6391b197c4ca"), user3);

        var thread1 = new Thread(Read1);
        var thread2 = new Thread(Read2);
        thread1.Start();
        thread2.Start();

    }

    private static async void Read1()
    {
        var user = await Client.Get<MockUser>(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"));
        Console.WriteLine($"{user.Name} {user.Age}");
    } 
    private static async void Read2()
    {
        var user = await Client.Get<MockUser>(Guid.Parse("1522688b-7eba-4709-99c1-6391b197c4ca"));
        Console.WriteLine($"{user.Name} {user.Age}");
    } 
}