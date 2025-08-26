using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string body);
    }
}
