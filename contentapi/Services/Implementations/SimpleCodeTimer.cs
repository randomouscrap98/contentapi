using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace contentapi.Services.Implementations
{
    public class StopwatchCodeTimeData : CodeTimeData
    {
        public Stopwatch Timer {get;set;} = new Stopwatch();
    }

    public class SimpleCodeTimer : ICodeTimer
    {
        public List<StopwatchCodeTimeData> queue = new List<StopwatchCodeTimeData>();
        public readonly object queueLock = new object();

        public TimeSpan EndTimer(CodeTimeData startData)
        {
            var result = (StopwatchCodeTimeData)startData;
            result.Timer.Stop();

            lock(queueLock)
                queue.Add(result);

            return result.Timer.Elapsed;
        }

        public CodeTimeData StartTimer(string name)
        {
            var result = new StopwatchCodeTimeData() { Name = name };
            result.Timer.Start();
            return result;
        }

        public async Task FlushData(string path)
        {
            IEnumerable<string> results = null;

            lock(queueLock)
            {
                results = queue.Select(x => $"{x.Name}: {x.Timer.ElapsedMilliseconds} ms [{x.Start}]").ToList();
                queue.Clear();
            }

            //This allows us to just throw away profiler data if we don't want to profile anymore
            if(!string.IsNullOrWhiteSpace(path))
                await File.AppendAllLinesAsync(path, results, System.Text.Encoding.UTF8);
        }
    }
}