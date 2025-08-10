using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class Storage
    {
        public static CloudStorageAccount Account =>
            CloudStorageAccount.Parse(CloudConfigurationManager.GetSetting("DataConnectionString"));

        public static CloudTable GetTable(string name)
        {
            var client = Account.CreateCloudTableClient();
            var t = client.GetTableReference(name);
            t.CreateIfNotExists();
            return t;
        }

        public static CloudQueue GetQueue(string name)
        {
            var client = Account.CreateCloudQueueClient();
            var q = client.GetQueueReference(name);
            q.CreateIfNotExists();
            return q;
        }

        public static CloudBlobContainer GetContainer(string name)
        {
            var client = Account.CreateCloudBlobClient();
            var c = client.GetContainerReference(name);
            c.CreateIfNotExists();
            return c;
        }
    }
}
