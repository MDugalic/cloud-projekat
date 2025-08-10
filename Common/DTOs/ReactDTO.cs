using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class ReactDTO
    {
        public enum ReactionType { Up, Down }

        public class ReactDto
        {
            public string DiscussionId { get; set; }
            public ReactionType Type { get; set; }
        }
    }
}
