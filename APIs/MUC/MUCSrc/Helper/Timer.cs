using System.Diagnostics;
using AzuAutoStore.APIs.MUC.MUCSrc.Data;

namespace AzuAutoStore.APIs.MUC.MUCSrc.Helper;
#if DEBUG
    public class Timer {
        private static readonly Stopwatch stopwatch = new Stopwatch();

        public static void Start(IPackage request) {
            request.PrintDebug();
            stopwatch.Restart();
        }

        public static void Stop(string method) {
            Log.LogDebug($"{method}: {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
        }
    }
#endif