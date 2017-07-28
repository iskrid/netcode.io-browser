﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace netcode.io.demoserver
{
    class Program
    {
        static readonly byte[] _privateKey = new byte[]
        {
            0x60, 0x6a, 0xbe, 0x6e, 0xc9, 0x19, 0x10, 0xea,
            0x9a, 0x65, 0x62, 0xf6, 0x6f, 0x2b, 0x30, 0xe4,
            0x43, 0x71, 0xd6, 0x2c, 0xd1, 0x99, 0x27, 0x26,
            0x6b, 0x3c, 0x60, 0xf4, 0xb7, 0x15, 0xab, 0xa1,
        };

        static bool running = true;

        static void Main(string[] args)
        {
            // Start web server.
            WebServer ws = new WebServer(SendResponse, "http://localhost:8080/");
            ws.Run();

            // Run netcode.io server in another thread.
            var netcodeThread = new Thread(NetcodeServer);
            netcodeThread.IsBackground = true;
            netcodeThread.Start();

            Console.WriteLine("netcode.io demo server started, open up http://localhost:8080/ to try it!");
            Console.ReadKey();
            running = false;
            ws.Stop();
        }

        private static void NetcodeServer()
        {
            NetcodeLibrary.SetLogLevel(NetcodeLogLevel.Debug);

            double time = 0f;
            double deltaTime = 1.0 / 60.0;

            var server = new Server(
                "[::]:40000",
                "[::1]:40000", 
                0x1122334455667788L, 
                _privateKey,
                0);

            byte[] packetData = null;

            server.Start(NetcodeLibrary.GetMaxClients());

            while (running)
            {
                server.Update(time);

                if (server.ClientConnected(0) && packetData != null)
                {
                    server.SendPacket(0, packetData);
                    packetData = null;
                }

                for (var clientIndex = 0; clientIndex < NetcodeLibrary.GetMaxClients(); clientIndex++)
                {
                    while (true)
                    {
                        var packet = server.ReceivePacket(clientIndex);
                        if (packet == null)
                        {
                            break;
                        }

                        Console.WriteLine(Encoding.ASCII.GetString(packet));
                        packetData = packet;
                    }
                }

                NetcodeLibrary.Sleep(deltaTime);

                time += deltaTime;
            }
            
            server.Dispose();
        }

        public static string SendResponse(HttpListenerRequest request)
        {
            if (request.Url.AbsolutePath == "/")
            {
                var asmPath = Assembly.GetExecutingAssembly().Location;
                var indexPath = Path.Combine(new FileInfo(asmPath).DirectoryName, "index.htm");
                using (var reader = new StreamReader(indexPath))
                {
                    return reader.ReadToEnd();
                }
            }

            if (request.Url.AbsolutePath == "/token")
            {
                var clientId = ulong.Parse(request.QueryString["clientId"]);
                var token = NetcodeLibrary.GenerateConnectTokenFromPrivateKey(
                    new[] { "[::1]:40000" },
                    30,
                    clientId,
                    0x1122334455667788L,
                    0,
                    _privateKey);
                return Convert.ToBase64String(token);
            }

            return "404 not found";
        }
    }
}
