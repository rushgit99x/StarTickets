# Stripe Payment Integration Setup

This document provides instructions for setting up Stripe payment integration in the StarTickets application.

## Prerequisites

1. A Stripe account (create one at [stripe.com](https://stripe.com))
2. Stripe API keys (Publishable and Secret keys)

## Configuration

### 1. Update appsettings.json

Replace the placeholder Stripe keys in `appsettings.json` with your actual Stripe keys:

```json
{
  "StripeSettings": {
    "PublishableKey": "pk_test_your_actual_publishable_key_here",
    "SecretKey": "sk_test_your_actual_secret_key_here",
    "WebhookSecret": "whsec_your_actual_webhook_secret_here"
  }
}
```

### 2. Get Your Stripe Keys

1. Log in to your [Stripe Dashboard](https://dashboard.stripe.com)
2. Go to **Developers** > **API keys**
3. Copy your **Publishable key** and **Secret key**
4. For webhook secret, go to **Developers** > **Webhooks** and create a new endpoint

### 3. Configure Webhooks

1. In your Stripe Dashboard, go to **Developers** > **Webhooks**
2. Click **Add endpoint**
3. Set the endpoint URL to: `https://yourdomain.com/Payment/Webhook`
4. Select these events to listen for:
   - `payment_intent.succeeded`
   - `payment_intent.payment_failed`
   - `checkout.session.completed`
   - `payment_intent.canceled`
5. Copy the webhook signing secret

## Database Migration

After adding the new payment models, you'll need to create and run a migration:

```bash
dotnet ef migrations add AddPaymentModels
dotnet ef database update
```

## Testing

### Test Mode
- Use test keys (starting with `pk_test_` and `sk_test_`)
- Use test card numbers from [Stripe's testing documentation](https://stripe.com/docs/testing)

### Test Card Numbers
- **Success**: 4242 4242 4242 4242
- **Decline**: 4000 0000 0000 0002
- **Insufficient funds**: 4000 0000 0000 9995

## Features Implemented

### 1. Payment Flow
- **Checkout**: Secure Stripe Checkout session creation
- **Success**: Payment confirmation and ticket generation
- **Cancel**: Payment cancellation handling
- **Webhook**: Real-time payment status updates

### 2. Security Features
- Webhook signature verification
- PCI compliance through Stripe
- Secure payment processing
- No card data storage

### 3. User Experience
- Modern, responsive payment interface
- Real-time payment status updates
- Email confirmations
- Mobile-friendly design

## API Endpoints

### Payment Controller
- `GET /Payment/Checkout/{bookingId}` - Start payment process
- `GET /Payment/Success` - Payment success page
- `GET /Payment/Cancel` - Payment cancellation page
- `POST /Payment/Webhook` - Stripe webhook handler

## Error Handling

The system includes comprehensive error handling for:
- Payment failures
- Network issues
- Invalid webhook signatures
- Database errors
- Stripe API errors

## Monitoring

Monitor your payments through:
1. Stripe Dashboard
2. Application logs
3. Database payment records
4. Webhook processing logs

## Production Deployment

Before going live:
1. Replace test keys with live keys
2. Update webhook endpoint URL
3. Test with real payment methods
4. Set up monitoring and alerts
5. Review security settings

## Support

For issues with:
- **Stripe integration**: Check Stripe documentation and logs
- **Application errors**: Review application logs
- **Payment processing**: Verify webhook configuration

## Security Notes

- Never commit real API keys to version control
- Use environment variables for production keys
- Regularly rotate API keys
- Monitor for suspicious activity
- Keep Stripe SDK updated