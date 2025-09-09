using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StarTickets.Data;
using StarTickets.Filters;
using StarTickets.Models;
using StarTickets.Models.ViewModels;

namespace StarTickets.Controllers
{
    [RoleAuthorize("3")] // Customer only
    public class BookingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BookingController> _logger;

        public BookingController(ApplicationDbContext context, ILogger<BookingController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Booking/BookTicket/5
        public async Task<IActionResult> BookTicket(int eventId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var eventEntity = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories.Where(tc => tc.IsActive))
                .FirstOrDefaultAsync(e => e.EventId == eventId && e.IsActive &&
                                          e.Status == EventStatus.Published);

            if (eventEntity == null)
            {
                TempData["ErrorMessage"] = "Event not found or not available for booking.";
                return RedirectToAction("Index", "Home");
            }

            // Check if event is in the future
            if (eventEntity.EventDate <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "This event has already occurred.";
                return RedirectToAction("Index", "Home");
            }

            // Get user information
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId.Value);

            var viewModel = new BookTicketViewModel
            {
                Event = eventEntity,
                TicketCategories = eventEntity.TicketCategories?.ToList() ?? new List<TicketCategory>(),
                CustomerEmail = user?.Email,
                CustomerFirstName = user?.FirstName,
                CustomerLastName = user?.LastName,
                CustomerPhone = user?.PhoneNumber
            };

            return View(viewModel);
        }

        // POST: Booking/ProcessBooking
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessBooking(BookTicketViewModel model)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            if (!ModelState.IsValid)
            {
                // Reload event data if validation fails
                await ReloadBookingViewModel(model);
                return View("BookTicket", model);
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // Validate event and ticket availability
                var eventEntity = await _context.Events
                    .Include(e => e.TicketCategories)
                    .FirstOrDefaultAsync(e => e.EventId == model.EventId && e.IsActive);

                if (eventEntity == null)
                {
                    ModelState.AddModelError("", "Event not found.");
                    await ReloadBookingViewModel(model);
                    return View("BookTicket", model);
                }

                decimal totalAmount = 0;
                var bookingDetails = new List<BookingDetail>();

                // Process each ticket category
                foreach (var selectedCategory in model.SelectedCategories.Where(sc => sc.Quantity > 0))
                {
                    var ticketCategory = eventEntity.TicketCategories?
                        .FirstOrDefault(tc => tc.TicketCategoryId == selectedCategory.TicketCategoryId);

                    if (ticketCategory == null)
                    {
                        ModelState.AddModelError("", $"Ticket category not found.");
                        await ReloadBookingViewModel(model);
                        return View("BookTicket", model);
                    }

                    // Check availability
                    if (ticketCategory.AvailableQuantity < selectedCategory.Quantity)
                    {
                        ModelState.AddModelError("",
                            $"Only {ticketCategory.AvailableQuantity} tickets available for {ticketCategory.CategoryName}.");
                        await ReloadBookingViewModel(model);
                        return View("BookTicket", model);
                    }

                    // Create booking detail
                    var bookingDetail = new BookingDetail
                    {
                        TicketCategoryId = selectedCategory.TicketCategoryId,
                        Quantity = selectedCategory.Quantity,
                        UnitPrice = ticketCategory.Price,
                        TotalPrice = ticketCategory.Price * selectedCategory.Quantity
                    };

                    bookingDetails.Add(bookingDetail);
                    totalAmount += bookingDetail.TotalPrice;

                    // Update available quantity
                    ticketCategory.AvailableQuantity -= selectedCategory.Quantity;
                }

                if (bookingDetails.Count == 0)
                {
                    ModelState.AddModelError("", "Please select at least one ticket.");
                    await ReloadBookingViewModel(model);
                    return View("BookTicket", model);
                }

                // Apply discount if promo code is provided
                decimal discountAmount = 0;
                if (!string.IsNullOrWhiteSpace(model.PromoCode))
                {
                    var promo = await _context.PromotionalCampaigns
                        .FirstOrDefaultAsync(p => p.DiscountCode == model.PromoCode &&
                                                 p.IsActive &&
                                                 DateTime.UtcNow >= p.StartDate &&
                                                 DateTime.UtcNow <= p.EndDate &&
                                                 (p.MaxUsage == null || p.CurrentUsage < p.MaxUsage));

                    if (promo != null)
                    {
                        // Replace all instances of:
                        // if (promo.DiscountType == DiscountType.Percentage)
                        // with:
                        if ((int)promo.DiscountType == (int)DiscountType.Percentage)
                        {
                            discountAmount = totalAmount * (promo.DiscountValue / 100);
                        }
                        else
                        {
                            discountAmount = Math.Min(promo.DiscountValue, totalAmount);
                        }

                        // Update promo usage
                        promo.CurrentUsage++;
                    }
                }

                decimal finalAmount = totalAmount - discountAmount;

                // Create booking
                var booking = new Booking
                {
                    BookingReference = GenerateBookingReference(),
                    CustomerId = userId.Value,
                    EventId = model.EventId,
                    BookingDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    PaymentStatus = PaymentStatus.Pending,
                    PromoCodeUsed = model.PromoCode,
                    Status = (Models.BookingStatus)BookingStatus.Active,
                    BookingDetails = bookingDetails,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Bookings.Add(booking);
                await _context.SaveChangesAsync();

                // Generate tickets for each booking detail
                foreach (var bookingDetail in bookingDetails)
                {
                    bookingDetail.BookingId = booking.BookingId;

                    for (int i = 0; i < bookingDetail.Quantity; i++)
                    {
                        var ticket = new Ticket
                        {
                            BookingDetailId = bookingDetail.BookingDetailId,
                            TicketNumber = GenerateTicketNumber(booking.BookingId, bookingDetail.BookingDetailId, i + 1),
                            QRCode = GenerateQRCode(booking.BookingReference, i + 1),
                            IsUsed = false,
                            CreatedAt = DateTime.UtcNow
                        };

                        _context.Tickets.Add(ticket);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["SuccessMessage"] = $"Booking successful! Your booking reference is {booking.BookingReference}";
                return RedirectToAction("BookingConfirmation", new { bookingId = booking.BookingId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing booking");
                ModelState.AddModelError("", "An error occurred while processing your booking. Please try again.");
                await ReloadBookingViewModel(model);
                return View("BookTicket", model);
            }
        }

        // GET: Booking/BookingConfirmation/5
        public async Task<IActionResult> BookingConfirmation(int bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var booking = await _context.Bookings
                .Include(b => b.Event)
                    .ThenInclude(e => e.Venue)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.TicketCategory)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Index", "Home");
            }

            return View(booking);
        }

        // POST: Booking/ValidatePromoCode
        [HttpPost]
        public async Task<IActionResult> ValidatePromoCode(string promoCode, decimal totalAmount)
        {
            try
            {
                var promo = await _context.PromotionalCampaigns
                    .FirstOrDefaultAsync(p => p.DiscountCode == promoCode &&
                                             p.IsActive &&
                                             DateTime.UtcNow >= p.StartDate &&
                                             DateTime.UtcNow <= p.EndDate &&
                                             (p.MaxUsage == null || p.CurrentUsage < p.MaxUsage));

                if (promo == null)
                {
                    return Json(new { valid = false, message = "Invalid or expired promo code." });
                }

                decimal discountAmount = 0;
                if ((int)promo.DiscountType == (int)DiscountType.Percentage)
                {
                    discountAmount = totalAmount * (promo.DiscountValue / 100);
                }
                else
                {
                    discountAmount = Math.Min(promo.DiscountValue, totalAmount);
                }

                return Json(new
                {
                    valid = true,
                    discountAmount = discountAmount,
                    finalAmount = totalAmount - discountAmount,
                    message = $"Promo code applied! You save ${discountAmount:F2}"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating promo code");
                return Json(new { valid = false, message = "Error validating promo code." });
            }
        }

        // Helper methods
        private async Task ReloadBookingViewModel(BookTicketViewModel model)
        {
            var eventEntity = await _context.Events
                .Include(e => e.Category)
                .Include(e => e.Venue)
                .Include(e => e.TicketCategories.Where(tc => tc.IsActive))
                .FirstOrDefaultAsync(e => e.EventId == model.EventId);

            if (eventEntity != null)
            {
                model.Event = eventEntity;
                model.TicketCategories = eventEntity.TicketCategories?.ToList() ?? new List<TicketCategory>();
            }
        }

        private string GenerateBookingReference()
        {
            return "BK" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                   new Random().Next(1000, 9999).ToString();
        }

        private string GenerateTicketNumber(int bookingId, int bookingDetailId, int ticketSequence)
        {
            return $"TK{bookingId:D6}{bookingDetailId:D3}{ticketSequence:D2}";
        }

        private string GenerateQRCode(string bookingReference, int ticketSequence)
        {
            return $"{bookingReference}-{ticketSequence:D2}";
        }
    }

    // Enums for booking
    public enum BookingStatus
    {
        Active,
        Cancelled
    }

    public enum DiscountType
    {
        Percentage,
        Fixed
    }
}