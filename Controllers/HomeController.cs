using Microsoft.AspNetCore.Mvc;
using StarTickets.Models;

namespace StarTickets.Controllers
{
    //public class HomeController : Controller
    //{
    //    private readonly ILogger<HomeController> _logger;

    //    public HomeController(ILogger<HomeController> logger)
    //    {
    //        _logger = logger;
    //    }

    //    public IActionResult Index()
    //    {
    //        return View();
    //    }

    //    public IActionResult Privacy()
    //    {
    //        return View();
    //    }

    //    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    //    public IActionResult Error()
    //    {
    //        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    //    }
    //}
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // If user is authenticated, redirect to appropriate dashboard
            if (User.Identity?.IsAuthenticated == true)
            {
                if (User.IsInRole(RoleConstants.Admin))
                    return RedirectToAction("Index", "Admin");
                if (User.IsInRole(RoleConstants.EventOrganizer))
                    return RedirectToAction("Index", "EventOrganizer");
                if (User.IsInRole(RoleConstants.Customer))
                    return RedirectToAction("Index", "Customer");
            }

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
