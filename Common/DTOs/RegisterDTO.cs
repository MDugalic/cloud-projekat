using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class RegisterDTO
    {
        public string FullName { get; set; }
        public string Gender { get; set; }    // optional: "M","F","Other"
        public string Country { get; set; }
        public string City { get; set; }
        public string Address { get; set; }

        public string Email { get; set; }
        public string Password { get; set; }

        // URL do slike (upload ćemo dodati kasnije; za početak može plain URL)
        public string PhotoUrl { get; set; }
    }
}
