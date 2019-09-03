using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;

namespace mqtt
{
    class Program
    {
        static int Main(string[] args)
        {
            Console.CursorVisible = false;

            // might want to run something async at some point, stick it on the thread pool
            var source = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                source.Cancel();
            };

            try
            {
                return MainAsync(args, source.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return 1223;
        }


        static async Task<int> MainAsync(string[] args, CancellationToken token)
        {
            MQTTBroker broker = new MQTTBroker();

            broker.Run();

            while (token.IsCancellationRequested == false)
            {
                broker.Loop();
            }

            Console.WriteLine("Interrupted by termination signal, closing down");

            broker.Stop();

            return await Task.FromResult(0);
        }
    }
}
