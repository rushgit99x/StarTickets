using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StarTickets.Data;
using StarTickets.Filters;
using StarTickets.Models;
using StarTickets.Models.ViewModels;
using StarTickets.Services;
using Stripe;

namespace StarTickets.Controllers
{
    [RoleAuthorize("3")] // Customer only
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IStripeService _stripeService;
        private readonly StripeSettings _stripeSettings;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            IStripeService stripeService,
            IOptions<StripeSettings> stripeSettings,
            ILogger<PaymentController> logger)
        {
            _context = context;
            _stripeService = stripeService;
            _stripeSettings = stripeSettings.Value;
            _logger = logger;
        }

        // GET: Payment/Checkout/5
        public async Task<IActionResult> Checkout(int bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.Customer)
                .Include(b => b.BookingDetails)
                    .ThenInclude(bd => bd.TicketCategory)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Index", "Home");
            }

            if (booking.PaymentStatus == PaymentStatus.Completed)
            {
                TempData["InfoMessage"] = "This booking has already been paid.";
                return RedirectToAction("BookingConfirmation", "Booking", new { bookingId = booking.BookingId });
            }

            try
            {
                var successUrl = Url.Action("Success", "Payment", new { bookingId = booking.BookingId }, Request.Scheme);
                var cancelUrl = Url.Action("Cancel", "Payment", new { bookingId = booking.BookingId }, Request.Scheme);

                var checkoutUrl = await _stripeService.CreateCheckoutSessionAsync(booking, successUrl, cancelUrl);

                var viewModel = new PaymentViewModel
                {
                    Booking = booking,
                    Event = booking.Event,
                    StripePublishableKey = _stripeSettings.PublishableKey,
                    CheckoutUrl = checkoutUrl,
                    TotalAmount = booking.FinalAmount,
                    Currency = "usd"
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating checkout session for booking {bookingId}");
                TempData["ErrorMessage"] = "Error processing payment. Please try again.";
                return RedirectToAction("BookTicket", "Booking", new { eventId = booking.EventId });
            }
        }

        // GET: Payment/Success
        public async Task<IActionResult> Success(int bookingId, string session_id)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            try
            {
                var booking = await _context.Bookings
                    .Include(b => b.Event)
                    .Include(b => b.Customer)
                    .Include(b => b.BookingDetails)
                        .ThenInclude(bd => bd.TicketCategory)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == userId.Value);

                if (booking == null)
                {
                    TempData["ErrorMessage"] = "Booking not found.";
                    return RedirectToAction("Index", "Home");
                }

                // Retrieve the session from Stripe
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                if (session.PaymentStatus == "paid")
                {
                    // Update booking payment status
                    booking.PaymentStatus = PaymentStatus.Completed;
                    booking.PaymentMethod = "card";
                    booking.PaymentTransactionId = session.PaymentIntentId;
                    booking.UpdatedAt = DateTime.UtcNow;

                    // Create payment record
                    var payment = new Payment
                    {
                        BookingId = booking.BookingId,
                        StripePaymentIntentId = session.PaymentIntentId,
                        StripeSessionId = session_id,
                        Amount = booking.FinalAmount,
                        Currency = "usd",
                        Status = PaymentStatus.Completed,
                        PaymentMethod = "card",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation($"Payment successful for booking {booking.BookingReference}, session {session_id}");

                    var viewModel = new PaymentSuccessViewModel
                    {
                        Booking = booking,
                        Event = booking.Event,
                        Payment = payment,
                        SessionId = session_id,
                        PaymentIntentId = session.PaymentIntentId
                    };

                    TempData["SuccessMessage"] = $"Payment successful! Your booking reference is {booking.BookingReference}";
                    return View(viewModel);
                }
                else
                {
                    TempData["ErrorMessage"] = "Payment was not completed successfully.";
                    return RedirectToAction("Checkout", new { bookingId = booking.BookingId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing payment success for booking {bookingId}");
                TempData["ErrorMessage"] = "Error processing payment confirmation.";
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Payment/Cancel
        public async Task<IActionResult> Cancel(int bookingId)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToAction("Login", "Auth");

            var booking = await _context.Bookings
                .Include(b => b.Event)
                .Include(b => b.Customer)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.CustomerId == userId.Value);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Booking not found.";
                return RedirectToAction("Index", "Home");
            }

            var viewModel = new PaymentCancelViewModel
            {
                Booking = booking,
                Event = booking.Event,
                Message = "Payment was cancelled. You can try again or contact support if you need assistance."
            };

            return View(viewModel);
        }

        // POST: Payment/Webhook
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Webhook()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].FirstOrDefault();

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    _stripeSettings.WebhookSecret
                );

                _logger.LogInformation($"Received Stripe webhook: {stripeEvent.Type}");

                // Store webhook for processing
                var webhook = new PaymentWebhook
                {
                    StripeEventId = stripeEvent.Id,
                    EventType = stripeEvent.Type,
                    Payload = json,
                    ReceivedAt = DateTime.UtcNow
                };

                _context.PaymentWebhooks.Add(webhook);
                await _context.SaveChangesAsync();

                // Process the webhook
                await ProcessWebhookAsync(stripeEvent);

                return Ok();
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe webhook signature verification failed");
                return BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook");
                return StatusCode(500);
            }
        }

        private async Task ProcessWebhookAsync(Event stripeEvent)
        {
            try
            {
                switch (stripeEvent.Type)
                {
                    case Events.PaymentIntentSucceeded:
                        await HandlePaymentIntentSucceeded(stripeEvent);
                        break;
                    case Events.PaymentIntentPaymentFailed:
                        await HandlePaymentIntentFailed(stripeEvent);
                        break;
                    case Events.CheckoutSessionCompleted:
                        await HandleCheckoutSessionCompleted(stripeEvent);
                        break;
                    default:
                        _logger.LogInformation($"Unhandled event type: {stripeEvent.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing webhook event {stripeEvent.Type}");
                throw;
            }
        }

        private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Completed;
                payment.UpdatedAt = DateTime.UtcNow;

                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == payment.BookingId);

                if (booking != null)
                {
                    booking.PaymentStatus = PaymentStatus.Completed;
                    booking.PaymentTransactionId = paymentIntent.Id;
                    booking.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Payment intent succeeded: {paymentIntent.Id}");
            }
        }

        private async Task HandlePaymentIntentFailed(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = paymentIntent.LastPaymentError?.Message;
                payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Payment intent failed: {paymentIntent.Id}");
            }
        }

        private async Task HandleCheckoutSessionCompleted(Event stripeEvent)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session == null) return;

            var bookingId = session.Metadata?.GetValueOrDefault("bookingId");
            if (bookingId != null && int.TryParse(bookingId, out int id))
            {
                var booking = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == id);

                if (booking != null && booking.PaymentStatus == PaymentStatus.Pending)
                {
                    booking.PaymentStatus = PaymentStatus.Completed;
                    booking.PaymentTransactionId = session.PaymentIntentId;
                    booking.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Checkout session completed for booking: {booking.BookingReference}");
                }
            }
        }
    }
}