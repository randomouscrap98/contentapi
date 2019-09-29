using System;
using System.Collections.Generic;
using contentapi.Configs;
using contentapi.Controllers;

namespace contentapi.test.Controllers
{
    public class UsersTestController : UsersController
    {
        public UsersTestController(GenericControllerServices services, UsersControllerConfig config,
                                   EmailConfig emailConfig) : base(services, config, emailConfig)
        {

        }

        public List<Tuple<string, string>> ConfirmationEmails = new List<Tuple<string, string>>();

        public override void SendConfirmationEmail(string recipient, string code)
        {
            ConfirmationEmails.Add(Tuple.Create(recipient, code));
        }
    }
}