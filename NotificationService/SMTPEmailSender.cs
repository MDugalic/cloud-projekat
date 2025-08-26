using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace NotificationService
{
    public class SMTPEmailSender : IEmailSender
    {
        private readonly string _host;
        private readonly int _port;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _from;

        public SMTPEmailSender(string host, int port, string user, string pass, string from)
        {
            _host = host;
            _port = port;
            _user = user;
            _pass = pass;
            _from = from;
        }

        public async Task SendAsync(string to, string subject, string body)
        {
            var message = new MailMessage(_from, to, subject, body);

            using(var client = new SmtpClient(_host, _port))
            {
                client.Credentials = new NetworkCredential(_user, _pass);
                client.EnableSsl = false;
                await client.SendMailAsync(message);
            }
        }
    }
}
