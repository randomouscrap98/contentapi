using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace contentapi.Services.Implementations
{
    public class StopwatchCodeTimeData : CodeTimeData
    {
        public Stopwatch Timer {get;set;} = new Stopwatch();
    }

    public class SimpleCodeTimerConfig
    {
        public bool LogImmediate {get;set;}
        //public string LogFlushPath {get;set;}
    }

    public class SimpleCodeTimer : ICodeTimer
    {
        //public List<StopwatchCodeTimeData> queue = new List<StopwatchCodeTimeData>();
        //public readonly object queueLock = new object();

        protected ILogger logger;
        protected SimpleCodeTimerConfig config;

        public SimpleCodeTimer(ILogger<SimpleCodeTimer> logger, SimpleCodeTimerConfig config)
        {
            this.logger = logger;
            this.config = config;
        }

        public string LogFormat(StopwatchCodeTimeData data)
        {
            return $"{data.Name}: {data.Timer.ElapsedMilliseconds} ms [{data.Start}]";
        }

        public TimeSpan EndTimer(CodeTimeData startData)
        {
            var result = (StopwatchCodeTimeData)startData;
            result.Timer.Stop();

            if(config.LogImmediate)
                logger.LogDebug(LogFormat(result));

            //lock(queueLock)
            //    queue.Add(result);

            return result.Timer.Elapsed;
        }

        public CodeTimeData StartTimer(string name)
        {
            var result = new StopwatchCodeTimeData() { Name = name };
            result.Timer.Start();
            return result;
        }

        //public async Task FlushData() //string path)
        //{
        //    IEnumerable<string> results = null;

        //    lock(queueLock)
        //    {
        //        results = queue.Select(x => LogFormat(x));
        //        queue.Clear();
        //    }

        //    //This allows us to just throw away profiler data if we don't want to profile anymore
        //    if(!string.IsNullOrWhiteSpace(config.LogFlushPath))
        //    {
        //        await File.AppendAllLinesAsync(config.LogFlushPath, results, System.Text.Encoding.UTF8);
        //    }
        //}
    }
}