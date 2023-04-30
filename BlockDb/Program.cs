// guid = 2522688b-7eba-4709-99c1-6391b197c4ca

using BlockDb;

var user1 = new MockUser("Vova", 23);
var user2 = new MockUser("Petya", 35);
var user3 = new MockUser("Lida", 46);
var client = BlockDbClient.Connect("C:\\Users\\DannyCW\\Desktop\\BlockDbStorage");
client.Put(Guid.Parse("1522688b-7eba-4709-99c1-6391b197c4ca"), user1);
client.Put(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"), user2);
client.Put(Guid.Parse("3522688b-7eba-4709-99c1-6391b197c4ca"), user3);
var user = client.Get<MockUser>(Guid.Parse("2522688b-7eba-4709-99c1-6391b197c4ca"));
Console.WriteLine($"{user.Name} {user.Age}");

public record MockUser(string Name, int Age);