using BrockAllen.MembershipReboot.Ef;
using BrockAllen.MembershipReboot.Relational;
using BrockAllen.MembershipReboot.WebHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;

namespace BrockAllen.MembershipReboot.Mvc.App_Start
{

    //public class MyEmailFormatter : IMessageFormatter<UserAccount>
    //{

    //    public Message Format(UserAccountEvent<UserAccount> accountEvent, IDictionary<string, string> values)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    public class MyEmailFormatter2 : EmailMessageFormatter
    {
       public MyEmailFormatter2(ApplicationInformation appInfo)
            : base(appInfo)
        {
        }
       public MyEmailFormatter2(Lazy<ApplicationInformation> appInfo)
            : base(appInfo)
        {
        }
        protected override string LoadBodyTemplate(UserAccountEvent<UserAccount> evt)
        {
            return LoadTemplate(CleanGenericName(evt.GetType()) + "_Body");
        }

        protected override string LoadSubjectTemplate(UserAccountEvent<UserAccount> evt)
        {
            return LoadTemplate(CleanGenericName(evt.GetType()) + "_Subject");
        }

        private string CleanGenericName(Type type)
        {
            var name = type.Name;
            var idx = name.IndexOf('`');
            if (idx > 0)
            {
                name = name.Substring(0, idx);
            }
            return name;
        }

        const string ResourcePathTemplate = "BrockAllen.MembershipReboot.Mvc.Templates.{0}.txt";
        string LoadTemplate(string name)
        {
            name = String.Format(ResourcePathTemplate, name);

            var asm = typeof(MyEmailFormatter2).Assembly;
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null) return null;
                using (var sr = new StreamReader(s))
                {
                    return sr.ReadToEnd();
                }
            }
        }
    }


    public class MembershipRebootConfig
    {
        public static MembershipRebootConfiguration Create()
        {
            var config = new MembershipRebootConfiguration();
            //config.RequireAccountVerification = false;
            config.AddEventHandler(new DebuggerEventHandler());

            var appinfo = new AspNetApplicationInformation("Test", "Test Email Signature",
                "UserAccount/Login", 
                "UserAccount/ChangeEmail/Confirm/",
                "UserAccount/Register/Cancel/",
                "UserAccount/PasswordReset/Confirm/");

            var emailFormatter = new MyEmailFormatter2(appinfo);
            // uncomment if you want email notifications -- also update smtp settings in web.config
            config.AddEventHandler(new EmailAccountEventsHandler(emailFormatter));

            // uncomment to enable SMS notifications -- also update TwilloSmsEventHandler class below
            //config.AddEventHandler(new TwilloSmsEventHandler(appinfo));
            
            // uncomment to ensure proper password complexity
            //config.ConfigurePasswordComplexity();

            var debugging = false;
#if DEBUG
            debugging = true;
#endif
            // this config enables cookies to be issued once user logs in with mobile code
            config.ConfigureTwoFactorAuthenticationCookies(debugging);

            return config;
        }
    }

    public class TwilloSmsEventHandler : SmsEventHandler
    {
        const string sid = "";
        const string token = "";
        const string fromPhone = "";
        
        public TwilloSmsEventHandler(ApplicationInformation appInfo)
            : base(new SmsMessageFormatter(appInfo))
        {
        }

        string Url
        {
            get
            {
                return String.Format("https://api.twilio.com/2010-04-01/Accounts/{0}/SMS/Messages", sid);
            }
        }

        string BasicAuthToken
        {
            get
            {
                var val = sid + ":" + token;
                var bytes = System.Text.Encoding.UTF8.GetBytes(val);
                val = Convert.ToBase64String(bytes);
                return val;
            }
        }

        HttpContent GetBody(Message msg)
        {
            var values = new KeyValuePair<string, string>[]
            { 
                new KeyValuePair<string, string>("From", fromPhone),
                new KeyValuePair<string, string>("To", msg.To),
                new KeyValuePair<string, string>("Body", msg.Body),
            };

            return new FormUrlEncodedContent(values);
        }

        protected override void SendSms(Message message)
        {
            if (!String.IsNullOrWhiteSpace(sid))
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", BasicAuthToken);
                var result = client.PostAsync(Url, GetBody(message)).Result;
                result.EnsureSuccessStatusCode();
            }
        }
    }
}