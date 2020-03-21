using System;
using contentapi.Configs;
using contentapi.Models;
using contentapi.Services;
using Microsoft.AspNetCore.Mvc;

namespace contentapi.test.Overrides
{
    public class TestSessionService : SessionService
    {
        public Func<long?> UidProvider = null;

        public TestSessionService(SessionConfig config) : base(config) { }

        //The important part: provide OUR uid
        public override long GetCurrentUid()
        {
            return UidProvider?.Invoke() ?? -1;
        }

    }
}