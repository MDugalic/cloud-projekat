using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public class FollowEntity : TableEntity
    {
        public FollowEntity(string discussionId, string userEmail)
        {
            PartitionKey = discussionId;
            RowKey = userEmail?.ToLowerInvariant();
        }

        public FollowEntity() { }
    }
}
