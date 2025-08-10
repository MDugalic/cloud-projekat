using Common;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdminToolsConsoleApp
{
    public class UsersTable
    {
        private readonly CloudTable _users = Storage.GetTable("Users");

        public UserEntity GetByEmail(string emailLower)
        {
            var res = _users.Execute(TableOperation.Retrieve<UserEntity>("User", emailLower));
            return res.Result as UserEntity;
        }

        public void Upsert(UserEntity user)
        {
            _users.Execute(TableOperation.InsertOrReplace(user));
        }
    }
}
