﻿using GammaLibrary.Extensions;
using System.Collections.Concurrent;
using System.Text.Json;

namespace OCRemoteServer
{
    public class RemoteManager
    {
        internal static ConcurrentQueue<(int, string)> commandQueue = new();
        static Dictionary<int, TaskCompletionSource<string>> handlers = new();
        static Dictionary<int, DateTime> times = new();
        static int commandId = 0;
        static readonly object locker = new();


        static RemoteManager()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    lock (locker)
                    {
                        foreach (var (i, time) in times.ToList())
                        {
                            if (DateTime.Now - time > TimeSpan.FromSeconds(60))
                            {
                                handlers[i].SetCanceled();
                                handlers.Remove(i);
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            });
        }


        public static Task<string> Request(string lua)
        {
            lock (locker)
            {
                commandId = (commandId + 1) % 1000;
                var cId = commandId;
                commandQueue.Enqueue((cId, lua));
                var tcs = new TaskCompletionSource<string>();
                handlers[cId] = tcs;
                return tcs.Task;
            }
        }

        public static void Callback(string response)
        {
            var list = JsonSerializer.Deserialize<Dictionary<int, string>>(response);
            lock (locker)
            {
                foreach (var (k, v) in list)
                {
                    var cId = k;
                    var reply = v;
                    var handler = handlers[cId];
                    handlers.Remove(cId);
                    if (reply.StartsWith("e$"))
                    {
                        var error = response.Substring(2);
                        handler.SetException(new Exception(error));
                    }
                    else
                    {
                        handler.SetResult(reply);
                    }
                }
            }
        }

    }

}
