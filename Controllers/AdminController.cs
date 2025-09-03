using Microsoft.AspNetCore.Mvc;
using StarTickets.Filters;

namespace StarTickets.Controllers
{
    public class AdminController : Controller
    {
        [RoleAuthorize("1")]
        public IActionResult Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            return View();
        }
    }
}
