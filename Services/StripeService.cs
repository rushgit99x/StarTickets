using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using StarTickets.Models;
using System.Text;

namespace StarTickets.Services
{
    public class StripeService : IStripeService
    {
        private readonly StripeSettings _stripeSettings;
        private readonly ILogger<StripeService> _logger;

        public StripeService(IOptions<StripeSettings> stripeSettings, ILogger<StripeService> logger)
        {
            _stripeSettings = stripeSettings.Value;
            _logger = logger;
            StripeConfiguration.ApiKey = _stripeSettings.SecretKey;
        }

        public async Task<string> CreateCheckoutSessionAsync(Booking booking, string successUrl, string cancelUrl)
        {
            try
            {
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string>
                    {
                        "card",
                    },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = (long)(booking.FinalAmount * 100), // Convert to cents
                                Currency = "usd",
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = $"Event Tickets - {booking.Event?.EventName}",
                                    Description = $"Booking Reference: {booking.BookingReference}",
                                },
                            },
                            Quantity = 1,
                        },
                    },
                    Mode = "payment",
                    SuccessUrl = successUrl,
                    CancelUrl = cancelUrl,
                    Metadata = new Dictionary<string, string>
                    {
                        { "bookingId", booking.BookingId.ToString() },
                        { "bookingReference", booking.BookingReference },
                        { "customerId", booking.CustomerId.ToString() },
                        { "eventId", booking.EventId.ToString() }
                    },
                    CustomerEmail = booking.Customer?.Email,
                    ExpiresAt = DateTime.UtcNow.AddHours(24), // Session expires in 24 hours
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                _logger.LogInformation($"Created Stripe checkout session {session.Id} for booking {booking.BookingReference}");
                return session.Url;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"Stripe error creating checkout session for booking {booking.BookingReference}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating checkout session for booking {booking.BookingReference}");
                throw;
            }
        }

        public async Task<bool> VerifyWebhookSignatureAsync(string payload, string signature)
        {
            try
            {
                var eventObject = EventUtility.ConstructEvent(
                    payload,
                    signature,
                    _stripeSettings.WebhookSecret
                );

                return eventObject != null;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Error verifying Stripe webhook signature");
                return false;
            }
        }

        public async Task<PaymentIntent> ProcessPaymentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                var paymentIntent = await service.GetAsync(paymentIntentId);
                return paymentIntent;
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"Error processing payment intent {paymentIntentId}");
                throw;
            }
        }

        public async Task<bool> RefundPaymentAsync(string paymentIntentId, decimal? amount = null)
        {
            try
            {
                var options = new RefundCreateOptions
                {
                    PaymentIntent = paymentIntentId,
                };

                if (amount.HasValue)
                {
                    options.Amount = (long)(amount.Value * 100); // Convert to cents
                }

                var service = new RefundService();
                var refund = await service.CreateAsync(options);

                _logger.LogInformation($"Created refund {refund.Id} for payment intent {paymentIntentId}");
                return refund.Status == "succeeded";
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"Error creating refund for payment intent {paymentIntentId}");
                return false;
            }
        }

        public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
        {
            try
            {
                var service = new PaymentIntentService();
                return await service.GetAsync(paymentIntentId);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, $"Error retrieving payment intent {paymentIntentId}");
                throw;
            }
        }
    }

    public class StripeSettings
    {
        public string PublishableKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
    }
}