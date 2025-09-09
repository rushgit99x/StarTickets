using StarTickets.Models;

namespace StarTickets.Services
{
    public interface IStripeService
    {
        Task<string> CreateCheckoutSessionAsync(Booking booking, string successUrl, string cancelUrl);
        Task<bool> VerifyWebhookSignatureAsync(string payload, string signature);
        Task<PaymentIntent> ProcessPaymentAsync(string paymentIntentId);
        Task<bool> RefundPaymentAsync(string paymentIntentId, decimal? amount = null);
        Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId);
    }
}