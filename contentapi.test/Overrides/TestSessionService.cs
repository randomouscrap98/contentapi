using System;
using contentapi.Configs;
using contentapi.Services;

namespace contentapi.test.Overrides
{
    public class TestSessionService : SessionService
    {
        public TestSessionService(SessionConfig config) : base(config) {}

        public Func<long?> UidProvider = null;

        public override string GetCurrentField(string field)
        {
            string result = null;

            if(field == UserIdField)
                result = UidProvider?.Invoke()?.ToString(); //DesiredUserId?.ToString();

            return ProcessFieldValue(field, result);
        }
    }
}