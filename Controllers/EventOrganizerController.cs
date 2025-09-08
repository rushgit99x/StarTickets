using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarTickets.Data;
using StarTickets.Filters;
using StarTickets.Models;
using StarTickets.Models.ViewModels;

namespace StarTickets.Controllers
{
    [RoleAuthorize("2")] // Event Organizer only
    public class EventOrganizerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EventOrganizerController> _logger;

        public EventOrganizerController(ApplicationDbContext context, ILogger<EventOrganizerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: EventOrganizer Dashboard
        public async Task<IActionResult> Index()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            // Get organizer's events for dashboard stats
            var organizerEvents = await _context.Events
                .Where(e => e.OrganizerId == userId)
                .Include(e => e.TicketCategories)
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                .ToListAsync();

            var dashboardData = new EventOrganizerDashboardViewModel
            {
                TotalEvents = organizerEvents.Count,
                ActiveEvents = organizerEvents.Count(e => e.Status == EventStatus.Published && e.IsActive),
                TotalTicketsSold = organizerEvents
                    .SelectMany(e => e.Bookings?.Where(b => b.PaymentStatus == PaymentStatus.Completed) ?? new List<Booking>())
                    .SelectMany(b => b.BookingDetails ?? new List<BookingDetail>())
                    .Sum(bd => bd.Quantity),
                TotalRevenue = organizerEvents
                    .SelectMany(e => e.Bookings?.Where(b => b.PaymentStatus == PaymentStatus.Completed) ?? new List<Booking>())
                    .Sum(b => b.FinalAmount),
                RecentEvents = organizerEvents
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(5)
                    .ToList()
            };

            return View(dashboardData);
        }

        // GET: EventOrganizer/MyEvents
        public async Task<IActionResult> MyEvents(string searchTerm = "", int categoryFilter = 0,
            EventStatus? statusFilter = null, int page = 1, int pageSize = 10)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var query = _context.Events
                .Where(e => e.OrganizerId == userId)
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories)
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                .AsQueryable();

            // Apply filters (same as admin)
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(e => e.EventName.Contains(searchTerm) ||
                                        e.Description!.Contains(searchTerm) ||
                                        e.Venue!.VenueName.Contains(searchTerm));
            }

            if (categoryFilter > 0)
            {
                query = query.Where(e => e.CategoryId == categoryFilter);
            }

            if (statusFilter.HasValue)
            {
                query = query.Where(e => e.Status == statusFilter.Value);
            }

            var totalEvents = await query.CountAsync();
            var events = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.EventCategories.ToListAsync();

            var viewModel = new EventOrganizerEventsViewModel
            {
                Events = events,
                Categories = categories,
                SearchTerm = searchTerm,
                CategoryFilter = categoryFilter,
                StatusFilter = statusFilter,
                CurrentPage = page,
                PageSize = pageSize,
                TotalEvents = totalEvents,
                TotalPages = (int)Math.Ceiling((double)totalEvents / pageSize)
            };

            return View(viewModel);
        }

        // GET: EventOrganizer/CreateEvent
        public async Task<IActionResult> CreateEvent()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var categories = await _context.EventCategories.Where(c => c.CategoryName != null).ToListAsync();
            var venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();

            var viewModel = new CreateEventOrganizerViewModel
            {
                Categories = categories,
                Venues = venues,
                EventDate = DateTime.Now.AddDays(7),
                EndDate = DateTime.Now.AddDays(7).AddHours(3),
                OrganizerId = userId.Value // Pre-fill with current user
            };

            return View(viewModel);
        }

        // POST: EventOrganizer/CreateEvent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateEvent(CreateEventOrganizerViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            // Ensure organizer can only create events for themselves
            model.OrganizerId = userId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    var eventEntity = new Event
                    {
                        EventName = model.EventName,
                        Description = model.Description,
                        EventDate = model.EventDate,
                        EndDate = model.EndDate,
                        VenueId = model.VenueId,
                        OrganizerId = model.OrganizerId,
                        CategoryId = model.CategoryId,
                        BandName = model.BandName,
                        Performer = model.Performer,
                        ImageUrl = model.ImageUrl,
                        Status = EventStatus.Draft, // Organizers create drafts, admins approve
                        IsActive = model.IsActive,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Events.Add(eventEntity);
                    await _context.SaveChangesAsync();

                    // Create ticket categories if provided
                    if (model.TicketCategories != null && model.TicketCategories.Any())
                    {
                        foreach (var ticketCat in model.TicketCategories.Where(tc => !string.IsNullOrWhiteSpace(tc.CategoryName)))
                        {
                            var ticketCategory = new TicketCategory
                            {
                                EventId = eventEntity.EventId,
                                CategoryName = ticketCat.CategoryName,
                                Price = ticketCat.Price,
                                TotalQuantity = ticketCat.TotalQuantity,
                                AvailableQuantity = ticketCat.TotalQuantity,
                                Description = ticketCat.Description,
                                IsActive = true,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.TicketCategories.Add(ticketCategory);
                        }
                        await _context.SaveChangesAsync();
                    }

                    TempData["SuccessMessage"] = "Event created successfully! It will be reviewed by administrators before publication.";
                    return RedirectToAction(nameof(MyEvents));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating event");
                    ModelState.AddModelError("", "An error occurred while creating the event. Please try again.");
                }
            }

            // Reload dropdown data if validation fails
            model.Categories = await _context.EventCategories.ToListAsync();
            model.Venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();

            return View(model);
        }

        // GET: EventOrganizer/EditEvent/5
        public async Task<IActionResult> EditEvent(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var eventEntity = await _context.Events
                .Include(e => e.TicketCategories)
                .FirstOrDefaultAsync(e => e.EventId == id && e.OrganizerId == userId);

            if (eventEntity == null)
            {
                TempData["ErrorMessage"] = "Event not found or you don't have permission to edit it.";
                return RedirectToAction(nameof(MyEvents));
            }

            // Check if event can be edited (not published or has bookings)
            var hasBookings = await _context.Bookings.AnyAsync(b => b.EventId == id);
            if (eventEntity.Status == EventStatus.Published && hasBookings)
            {
                TempData["ErrorMessage"] = "Cannot edit published events with existing bookings.";
                return RedirectToAction(nameof(MyEvents));
            }

            var categories = await _context.EventCategories.ToListAsync();
            var venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();

            var viewModel = new EditEventOrganizerViewModel
            {
                EventId = eventEntity.EventId,
                EventName = eventEntity.EventName,
                Description = eventEntity.Description,
                EventDate = eventEntity.EventDate,
                EndDate = eventEntity.EndDate,
                VenueId = eventEntity.VenueId,
                CategoryId = eventEntity.CategoryId,
                BandName = eventEntity.BandName,
                Performer = eventEntity.Performer,
                ImageUrl = eventEntity.ImageUrl,
                IsActive = eventEntity.IsActive,
                Categories = categories,
                Venues = venues,
                TicketCategories = eventEntity.TicketCategories?.Select(tc => new TicketCategoryViewModel
                {
                    TicketCategoryId = tc.TicketCategoryId,
                    CategoryName = tc.CategoryName,
                    Price = tc.Price,
                    TotalQuantity = tc.TotalQuantity,
                    AvailableQuantity = tc.AvailableQuantity,
                    Description = tc.Description,
                    IsActive = tc.IsActive
                }).ToList() ?? new List<TicketCategoryViewModel>()
            };

            return View(viewModel);
        }

        // POST: EventOrganizer/EditEvent/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditEvent(EditEventOrganizerViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            if (ModelState.IsValid)
            {
                try
                {
                    var eventEntity = await _context.Events
                        .Include(e => e.TicketCategories)
                        .FirstOrDefaultAsync(e => e.EventId == model.EventId && e.OrganizerId == userId);

                    if (eventEntity == null)
                    {
                        TempData["ErrorMessage"] = "Event not found or you don't have permission to edit it.";
                        return RedirectToAction(nameof(MyEvents));
                    }

                    // Update event properties
                    eventEntity.EventName = model.EventName;
                    eventEntity.Description = model.Description;
                    eventEntity.EventDate = model.EventDate;
                    eventEntity.EndDate = model.EndDate;
                    eventEntity.VenueId = model.VenueId;
                    eventEntity.CategoryId = model.CategoryId;
                    eventEntity.BandName = model.BandName;
                    eventEntity.Performer = model.Performer;
                    eventEntity.ImageUrl = model.ImageUrl;
                    eventEntity.IsActive = model.IsActive;
                    eventEntity.UpdatedAt = DateTime.UtcNow;

                    // Update ticket categories
                    if (model.TicketCategories != null)
                    {
                        // Remove existing ticket categories
                        var existingCategories = eventEntity.TicketCategories?.ToList() ?? new List<TicketCategory>();
                        _context.TicketCategories.RemoveRange(existingCategories);

                        // Add updated ticket categories
                        foreach (var ticketCat in model.TicketCategories.Where(tc => !string.IsNullOrWhiteSpace(tc.CategoryName)))
                        {
                            var ticketCategory = new TicketCategory
                            {
                                EventId = eventEntity.EventId,
                                CategoryName = ticketCat.CategoryName,
                                Price = ticketCat.Price,
                                TotalQuantity = ticketCat.TotalQuantity,
                                AvailableQuantity = ticketCat.AvailableQuantity,
                                Description = ticketCat.Description,
                                IsActive = ticketCat.IsActive,
                                CreatedAt = DateTime.UtcNow
                            };

                            _context.TicketCategories.Add(ticketCategory);
                        }
                    }

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Event updated successfully!";
                    return RedirectToAction(nameof(MyEvents));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating event");
                    ModelState.AddModelError("", "An error occurred while updating the event. Please try again.");
                }
            }

            // Reload dropdown data if validation fails
            model.Categories = await _context.EventCategories.ToListAsync();
            model.Venues = await _context.Venues.Where(v => v.IsActive).ToListAsync();

            return View(model);
        }

        // GET: EventOrganizer/EventDetails/5
        public async Task<IActionResult> EventDetails(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var eventEntity = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories)
                .Include(e => e.Bookings)
                    .ThenInclude(b => b.BookingDetails)
                .FirstOrDefaultAsync(e => e.EventId == id && e.OrganizerId == userId);

            if (eventEntity == null)
            {
                TempData["ErrorMessage"] = "Event not found or you don't have permission to view it.";
                return RedirectToAction(nameof(MyEvents));
            }

            // Calculate event statistics
            var totalTicketsSold = eventEntity.Bookings?
                .Where(b => b.PaymentStatus == PaymentStatus.Completed)
                .SelectMany(b => b.BookingDetails!)
                .Sum(bd => bd.Quantity) ?? 0;

            var totalRevenue = eventEntity.Bookings?
                .Where(b => b.PaymentStatus == PaymentStatus.Completed)
                .Sum(b => b.FinalAmount) ?? 0;

            var totalCapacity = eventEntity.TicketCategories?.Sum(tc => tc.TotalQuantity) ?? 0;

            var viewModel = new EventOrganizerDetailsViewModel
            {
                Event = eventEntity,
                TotalTicketsSold = totalTicketsSold,
                TotalRevenue = totalRevenue,
                TotalCapacity = totalCapacity,
                TicketsSoldPercentage = totalCapacity > 0 ? (double)totalTicketsSold / totalCapacity * 100 : 0
            };

            return View(viewModel);
        }

        // POST: EventOrganizer/SubmitForApproval/5
        [HttpPost]
        public async Task<IActionResult> SubmitForApproval(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Json(new { success = false, message = "User not found." });

            try
            {
                var eventEntity = await _context.Events.FirstOrDefaultAsync(e => e.EventId == id && e.OrganizerId == userId);
                if (eventEntity == null)
                {
                    return Json(new { success = false, message = "Event not found or permission denied." });
                }

                if (eventEntity.Status != EventStatus.Draft)
                {
                    return Json(new { success = false, message = "Only draft events can be submitted for approval." });
                }

                eventEntity.Status = EventStatus.Published; // Or create a "PendingApproval" status
                eventEntity.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Event submitted for approval successfully!" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting event for approval");
                return Json(new { success = false, message = "An error occurred while submitting the event." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Ensure this is included
        public async Task<IActionResult> DeleteEvent(int id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                _logger.LogWarning("DeleteEvent: User not found in session.");
                return Json(new { success = false, message = "User not found." });
            }

            try
            {
                _logger.LogInformation($"DeleteEvent: Attempting to delete event ID {id} for user ID {userId}");
                var eventEntity = await _context.Events
                    .Include(e => e.Bookings)
                    .Include(e => e.TicketCategories)
                    .FirstOrDefaultAsync(e => e.EventId == id && e.OrganizerId == userId);

                if (eventEntity == null)
                {
                    _logger.LogWarning($"DeleteEvent: Event ID {id} not found or permission denied for user ID {userId}");
                    return Json(new { success = false, message = "Event not found or permission denied." });
                }

                if (eventEntity.Bookings?.Any() == true)
                {
                    _logger.LogWarning($"DeleteEvent: Event ID {id} has bookings and cannot be deleted.");
                    return Json(new { success = false, message = "Cannot delete event with existing bookings." });
                }

                if (eventEntity.TicketCategories?.Any() == true)
                {
                    _logger.LogInformation($"DeleteEvent: Removing {eventEntity.TicketCategories.Count} ticket categories for event ID {id}");
                    _context.TicketCategories.RemoveRange(eventEntity.TicketCategories);
                }

                _logger.LogInformation($"DeleteEvent: Removing event ID {id}");
                _context.Events.Remove(eventEntity);
                int rowsAffected = await _context.SaveChangesAsync();
                _logger.LogInformation($"DeleteEvent: Successfully deleted event ID {id}. Rows affected: {rowsAffected}");

                return Json(new { success = true, message = "Event deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"DeleteEvent: Error deleting event ID {id}");
                return Json(new { success = false, message = "An error occurred while deleting the event: " + ex.Message });
            }
        }
        [HttpGet]
        [Route("EventOrganizer/Logout")]
        public async Task<IActionResult> Logout()
        {
            //await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login", "Auth");
            // Or redirect to home: return RedirectToAction("Index", "Home");
        }
    }
}