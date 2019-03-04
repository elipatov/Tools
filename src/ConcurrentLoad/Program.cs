using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Libs.Extensions;

namespace ConcurrentLoad
{
    class Program
    {
        private static readonly HttpClient _client = new HttpClient();
        private static readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
        private static readonly List<CancellationTokenSource> _cancellations = new List<CancellationTokenSource>();
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1000);
        private static ConcurrentBag<long> _times = new ConcurrentBag<long>();
        private static int _requests;
        private static int _errors;
        

        static void Main(string[] args)
        {
            Console.Write(String.Empty); // write nothing, but causes Console.Out to be initialized

            int initialThreads = 10;
            for (int i = 0; i < initialThreads; ++i)
            {
                StartSend().NoWait();
            }

            Console.WriteLine("Running {0} threads...", _cancellations.Count);

            Monitor().NoWait();

            while (!_cancellation.IsCancellationRequested)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                switch (key.Key)
                {
                    case ConsoleKey.Add:
                    case ConsoleKey.OemPlus:
                        StartSend().NoWait();
                        Console.WriteLine("Running {0} threads...", _cancellations.Count);
                        break;
                    case ConsoleKey.Subtract:
                    case ConsoleKey.OemMinus:
                        if (_cancellations.Count > 0)
                        {
                            _cancellations[0].Cancel();
                            _cancellations.RemoveAt(0);
                            Console.WriteLine("Running {0} threads...", _cancellations.Count);
                        }
                        break;
                    case ConsoleKey.Escape:
                        if (Console.ReadKey(true).Key == ConsoleKey.Escape)
                        {
                            _cancellations.ForEach(c => c.Cancel());
                            _cancellation.Cancel();
                        }
                        break;
                }
            }
            _cancellation.Cancel();
        }

        private static async Task StartSend()
        {
            var cancelation = new CancellationTokenSource();
            _cancellations.Add(cancelation);
            await Task.Yield();

            using (var client = new HttpClient())
            {
                while (!cancelation.IsCancellationRequested)
                {
                    try
                    {
                        await _semaphore.WaitAsync(cancelation.Token).ConfigureAwait(false);
                        var content =
                            new StringContent(
                                $"{{Operations:[{{Operation: 'replace', PropertyName: 'fee', Value: {Environment.TickCount % 10} }}]}}");
                        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                        var request = new HttpRequestMessage(HttpMethod.Get,
                            new Uri("http://api.xyz.com/v1/api"))
                        {
                            Headers =
                            {
                                {
                                    "Authorization",
                                    "Bearer eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.<payload>.LYD4gMFcaGkYrJtgkMt0wjPfcihyO93buTpF2tC1oFg"
                                }
                            },
                            //Content = content
                        };
                        var sw = Stopwatch.StartNew();
                        var cts = new CancellationTokenSource();
                        var response = await client.SendAsync(request, cts.Token).ConfigureAwait(false);
                        sw.Stop();
                        Volatile.Read(ref _times).Add(sw.ElapsedMilliseconds);
                        Interlocked.Increment(ref _requests);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref _errors);
                    }
                    _semaphore.Release();
                }
            }

        }

        private static async Task Monitor()
        {
            await Task.Yield();
            int startRequests = Volatile.Read(ref _requests);
            int startErrors = Volatile.Read(ref _errors);
            DateTime starTime = DateTime.UtcNow;

            while (!_cancellation.IsCancellationRequested)
            {
                await Task.Delay(5000).ConfigureAwait(false);
                int currentRequests = Volatile.Read(ref _requests);
                int currentErrors = Volatile.Read(ref _errors);
                DateTime currentTime = DateTime.UtcNow;
                ConcurrentBag<long> times = Interlocked.Exchange(ref _times, new ConcurrentBag<long>());

                double seconds = (currentTime - starTime).TotalSeconds;
                double requestsPerSecond = ((double)currentRequests - startRequests) / seconds;
                double errorsPerSecond = ((double)currentErrors - startErrors) / seconds;
                double averageTime = times.Count > 0 ? times.Average() : double.NaN;
                Console.WriteLine("Requessts Per Second: {0:000.000}; Errors Per Second: {1:000.000}; Average time: {2:0000.0}",
                    requestsPerSecond, errorsPerSecond, averageTime);

                startRequests = currentRequests;
                startErrors = currentErrors;
                starTime = currentTime;
            }

        }
    }
}
