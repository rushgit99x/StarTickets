using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarTickets.Data;
using StarTickets.Models;
using StarTickets.Models.ViewModels;
using System.Diagnostics;

namespace StarTickets.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // If user is authenticated, redirect to appropriate dashboard
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetInt32("UserRole");

            if (userId.HasValue && userRole.HasValue)
            {
                switch (userRole.Value)
                {
                    case 1: // Admin
                        return RedirectToAction("Index", "Admin");
                    case 2: // Event Organizer
                        return RedirectToAction("Index", "EventOrganizer");
                    case 3: // Customer
                        return RedirectToAction("Index", "Customer");
                }
            }

            // Get featured events for homepage
            var featuredEvents = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories)
                .Where(e => e.IsActive && e.Status == EventStatus.Published && e.EventDate > DateTime.UtcNow)
                .OrderBy(e => e.EventDate)
                .Take(6)
                .ToListAsync();

            // Get event categories
            var categories = await _context.EventCategories
                .Include(c => c.Events!.Where(e => e.IsActive && e.Status == EventStatus.Published))
                .ToListAsync();

            var viewModel = new HomeViewModel
            {
                FeaturedEvents = featuredEvents,
                Categories = categories,
                IsAuthenticated = userId.HasValue
            };

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> SearchEvents(string query, int? categoryId, string location, DateTime? date)
        {
            var eventsQuery = _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories)
                .Where(e => e.IsActive && e.Status == EventStatus.Published && e.EventDate > DateTime.UtcNow);

            // Apply search filters
            if (!string.IsNullOrWhiteSpace(query))
            {
                eventsQuery = eventsQuery.Where(e =>
                    e.EventName.Contains(query) ||
                    e.Description!.Contains(query) ||
                    e.BandName!.Contains(query) ||
                    e.Performer!.Contains(query));
            }

            if (categoryId.HasValue && categoryId > 0)
            {
                eventsQuery = eventsQuery.Where(e => e.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(location))
            {
                eventsQuery = eventsQuery.Where(e =>
                    e.Venue!.City.Contains(location) ||
                    e.Venue!.VenueName.Contains(location));
            }

            if (date.HasValue)
            {
                var startDate = date.Value.Date;
                var endDate = startDate.AddDays(1);
                eventsQuery = eventsQuery.Where(e => e.EventDate >= startDate && e.EventDate < endDate);
            }

            var events = await eventsQuery
                .OrderBy(e => e.EventDate)
                .Take(20)
                .ToListAsync();

            return Json(new
            {
                success = true,
                events = events.Select(e => new {
                    id = e.EventId,
                    name = e.EventName,
                    description = e.Description,
                    date = e.EventDate.ToString("MMM dd, yyyy"),
                    time = e.EventDate.ToString("hh:mm tt"),
                    venue = e.Venue?.VenueName,
                    city = e.Venue?.City,
                    category = e.Category?.CategoryName,
                    image = e.ImageUrl,
                    minPrice = e.TicketCategories?.Min(tc => tc.Price) ?? 0
                })
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetEventsByCategory(int categoryId)
        {
            var events = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories)
                .Where(e => e.CategoryId == categoryId && e.IsActive &&
                           e.Status == EventStatus.Published && e.EventDate > DateTime.UtcNow)
                .OrderBy(e => e.EventDate)
                .Take(10)
                .ToListAsync();

            return Json(new
            {
                success = true,
                events = events.Select(e => new {
                    id = e.EventId,
                    name = e.EventName,
                    date = e.EventDate.ToString("MMM dd, yyyy"),
                    time = e.EventDate.ToString("hh:mm tt"),
                    venue = e.Venue?.VenueName,
                    city = e.Venue?.City,
                    image = e.ImageUrl,
                    minPrice = e.TicketCategories?.Min(tc => tc.Price) ?? 0
                })
            });
        }

        [HttpPost]
        public async Task<IActionResult> Subscribe(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Email is required" });
            }

            try
            {
                // Here you would typically save to a newsletter subscribers table
                // For now, we'll just simulate success
                _logger.LogInformation($"Newsletter subscription: {email}");

                return Json(new { success = true, message = "Successfully subscribed!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to newsletter");
                return Json(new { success = false, message = "An error occurred. Please try again." });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}

// View Models
namespace StarTickets.Models.ViewModels
{
    public class HomeViewModel
    {
        public List<Event> FeaturedEvents { get; set; } = new List<Event>();
        public List<EventCategory> Categories { get; set; } = new List<EventCategory>();
        public bool IsAuthenticated { get; set; }
    }
}

// Add these missing enums and constants if not already present
//public enum EventStatus
//{
//    Draft = 0,
//    Published = 1,
//    Cancelled = 2
//}

//public enum PaymentStatus
//{
//    Pending,
//    Completed,
//    Failed,
//    Refunded
//}

//public static class RoleConstants
//{
//    public const string Admin = "1";
//    public const string EventOrganizer = "2";
//    public const string Customer = "3";
//}