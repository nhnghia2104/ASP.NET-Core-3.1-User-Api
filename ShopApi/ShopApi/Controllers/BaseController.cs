using Microsoft.AspNetCore.Mvc;
using ShopApi.Entity;

namespace ShopApi.Controllers
{
    [Controller]
    public abstract class BaseController : ControllerBase
    {
        // returns the current authenticated account (null if not logged in)
        public User User => (User)HttpContext.Items["User"];
    }
}
