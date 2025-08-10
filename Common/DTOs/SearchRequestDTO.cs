using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common.DTOs
{
    public class SearchRequestDTO
    {
        public enum SortBy
        {
            None = 0,
            ScoreDesc,   // (Positive - Negative) opadajuće
            ScoreAsc,
            ImdbDesc,
            ImdbAsc,
            Newest,
            Oldest
        }

        public class SearchRequest
        {
            public string TitleContains { get; set; }
            public string GenreEquals { get; set; }
            public SortBy SortBy { get; set; }

            // Paginacija (dodaćemo kasnije continuation token ako želiš pravi Table paging)
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 10;
        }
    }
}
