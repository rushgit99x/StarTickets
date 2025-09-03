using Microsoft.AspNetCore.Mvc;
using StarTickets.Filters;

namespace StarTickets.Controllers
{
    [RoleAuthorize("3")]
    public class CustomerController : Controller
    {
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            return View();
        }
    }
}
