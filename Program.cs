using System;
using System.Net;
using System.Net.Sockets;

namespace Server
{
    class Program
    {
        static void Main(string[] args)
        {
            IPAddress address = IPAddress.Parse(Server.Host);// конвертация строки в ip
            Server.ServerSocket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);//сокет, использующий протокол Tcp
            Server.ServerSocket.ReceiveBufferSize = 0xFFFFFFF;
            Server.ServerSocket.SendBufferSize = 0xFFFFFFF;
            Server.ServerSocket.Bind(new IPEndPoint(address, Server.Port));// связали сокет с локальной конечной точкой
            Server.ServerSocket.Listen(100);// начинаем прослушивание входящих запросов
            Console.WriteLine($"Server has been started on {Server.Host}:{Server.Port}");
            Console.WriteLine("Waiting connections...");
            while(Server.Work)// пока work == true работаем
            {
                Socket handle = Server.ServerSocket.Accept();// новый объект socket для обработки входящего подключения
                Console.WriteLine($"New connection: {handle.RemoteEndPoint.ToString()}");
                new User(handle);// обслуживаем его в новом потоке

            }
            Console.WriteLine("Server closeing...");
        }
    }
}
