using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using Queryable.Sample.Models;
using Queryable.Filters;

namespace Queryable.Sample.Controllers
{
    [ApiController]
    [Route("{controller}/{action?}")]
    public class UsersController : ControllerBase
    {
        private ILogger _loggerFactory;
        private List<UserModel> _user;

        public UsersController(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory.CreateLogger(nameof(UsersController));
            _user = new List<UserModel>()
            {
                new UserModel(1, "ahmed", "ahmed@enjaz.tech", "+9647717225745"),
                new UserModel(2, "ali", "ali@enjaz.tech", "+9647713846278"),
                new UserModel(3, "mohamed", "mohamed@enjaz.tech", "+9647722948219"),
                new UserModel(4, "qasim", "qasim@enjaz.tech", "+9647802210088"),
                new UserModel(5, "haithem", "haithem@enjaz.tech", "+9647804948299"),
                new UserModel(6, "rasol", "rasol@enjaz.tech", "+9647729489914")
            };
        }

        [HttpGet("")]
        [Queryable]
        public IEnumerable Get()
        {
            return _user;
        }


    }
}