using Microsoft.EntityFrameworkCore;
using StarTickets.Data;
using StarTickets.Models;
using Stripe;

namespace StarTickets.Services
{
    public interface IPaymentWebhookService
    {
        Task ProcessWebhookAsync(Event stripeEvent);
        Task<bool> IsWebhookProcessedAsync(string eventId);
        Task MarkWebhookAsProcessedAsync(string eventId);
    }

    public class PaymentWebhookService : IPaymentWebhookService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PaymentWebhookService> _logger;

        public PaymentWebhookService(ApplicationDbContext context, ILogger<PaymentWebhookService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task ProcessWebhookAsync(Event stripeEvent)
        {
            try
            {
                // Check if webhook was already processed
                if (await IsWebhookProcessedAsync(stripeEvent.Id))
                {
                    _logger.LogInformation($"Webhook {stripeEvent.Id} already processed, skipping");
                    return;
                }

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
                    case Events.PaymentIntentCanceled:
                        await HandlePaymentIntentCanceled(stripeEvent);
                        break;
                    default:
                        _logger.LogInformation($"Unhandled webhook event type: {stripeEvent.Type}");
                        break;
                }

                await MarkWebhookAsProcessedAsync(stripeEvent.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing webhook {stripeEvent.Id}");
                throw;
            }
        }

        public async Task<bool> IsWebhookProcessedAsync(string eventId)
        {
            return await _context.PaymentWebhooks
                .AnyAsync(w => w.StripeEventId == eventId && w.Processed);
        }

        public async Task MarkWebhookAsProcessedAsync(string eventId)
        {
            var webhook = await _context.PaymentWebhooks
                .FirstOrDefaultAsync(w => w.StripeEventId == eventId);

            if (webhook != null)
            {
                webhook.Processed = true;
                webhook.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        private async Task HandlePaymentIntentSucceeded(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Completed;
                payment.UpdatedAt = DateTime.UtcNow;

                var booking = payment.Booking;
                if (booking != null)
                {
                    booking.PaymentStatus = PaymentStatus.Completed;
                    booking.PaymentTransactionId = paymentIntent.Id;
                    booking.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Payment intent succeeded: {paymentIntent.Id} for booking {booking?.BookingReference}");
            }
        }

        private async Task HandlePaymentIntentFailed(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = paymentIntent.LastPaymentError?.Message;
                payment.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Payment intent failed: {paymentIntent.Id} for booking {payment.Booking?.BookingReference}");
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

                    // Create payment record if it doesn't exist
                    var existingPayment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId);

                    if (existingPayment == null)
                    {
                        var payment = new Payment
                        {
                            BookingId = booking.BookingId,
                            StripePaymentIntentId = session.PaymentIntentId,
                            StripeSessionId = session.Id,
                            Amount = booking.FinalAmount,
                            Currency = "usd",
                            Status = PaymentStatus.Completed,
                            PaymentMethod = "card",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        _context.Payments.Add(payment);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation($"Checkout session completed for booking: {booking.BookingReference}");
                }
            }
        }

        private async Task HandlePaymentIntentCanceled(Event stripeEvent)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            if (paymentIntent == null) return;

            var payment = await _context.Payments
                .Include(p => p.Booking)
                .FirstOrDefaultAsync(p => p.StripePaymentIntentId == paymentIntent.Id);

            if (payment != null)
            {
                payment.Status = PaymentStatus.Failed;
                payment.FailureReason = "Payment was canceled by user";
                payment.UpdatedAt = DateTime.UtcNow;

                var booking = payment.Booking;
                if (booking != null)
                {
                    booking.PaymentStatus = PaymentStatus.Failed;
                    booking.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation($"Payment intent canceled: {paymentIntent.Id} for booking {booking?.BookingReference}");
            }
        }
    }
}