using System;
using System.Collections.Generic;
using contentapi.Configs;
using contentapi.Controllers;
using contentapi.Services;

namespace contentapi.test.Controllers
{
    public class TestSessionService : SessionService
    {
        public long? DesiredUserId;

        public TestSessionService(SessionConfig config) : base(config) {}

        public override string GetCurrentField(string field)
        {
            string result = null;

            if(field == UserIdField)
                result = DesiredUserId?.ToString();

            return ProcessFieldValue(field, result);
        }
    }

    public class UsersTestController : UsersController
    {
        public long? DesiredUserId
        {
            get { return ((TestSessionService)sessionService).DesiredUserId; }
            set { ((TestSessionService)sessionService).DesiredUserId = value; }
        }

        public UsersTestController(GenericControllerServices services, UsersControllerConfig config,
                                   EmailConfig emailConfig, TestSessionService testSession) : base(services, config, emailConfig)
        {
            //Force session service to be the tester (probably bad design but whatever)
            sessionService = testSession;
        }

        public static List<Tuple<string, string>> ConfirmationEmails = new List<Tuple<string, string>>();

        public override void SendConfirmationEmail(string recipient, string code)
        {
            ConfirmationEmails.Add(Tuple.Create(recipient, code));
        }
    }
}