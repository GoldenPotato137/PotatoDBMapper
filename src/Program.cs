// See https://aka.ms/new-console-template for more information

Console.WriteLine("Hello, World!");

using var stringWriter = new StreamWriter("./db/tmp.txt");
stringWriter.WriteLine("Hello, World!");