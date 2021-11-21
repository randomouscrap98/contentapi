using System;

namespace contentapi.Services
{
    public class CodeTimeData
    {
        public Guid Id {get;set;} = Guid.NewGuid();
        public string Name {get;set;}
        public DateTime Start {get;set;} = DateTime.Now;
    }

    public interface ICodeTimer
    {
        CodeTimeData StartTimer(string name);
        TimeSpan EndTimer(CodeTimeData startData);
    }
}