using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Queryable.Sample.Models
{
    public class UserModel
    {
        public UserModel(int id, string username, string email, string mobileNumber)
        {
            Id = id;
            Username = username;
            Email = email;
            MobileNumber = mobileNumber;
        }

        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string MobileNumber { get; set; }
    }
}