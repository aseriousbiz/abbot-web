using System.Collections.Generic;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Billing;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure.Telemetry;
using Serious.Abbot.Models;
using Stripe;
using Stripe.Checkout;
using Xunit;

public class BillingServiceTests
{
    public class TheUpdateSubscriptionAsyncMethod
    {
        [Theory]
        [InlineData(PlanType.Business, 1, 1)]
        [InlineData(PlanType.Business, 1, 2)]
        [InlineData(PlanType.Unlimited, 1, 2)]
        public async Task DoesNotModifyPlanIfNoPlanTypeInProduct(PlanType organizationPlan, int existingSeats, int purchasedSeats)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.StripeCustomerId = "STRIPECUSTOMERID123";
            organization.PlanType = organizationPlan;
            organization.PurchasedSeatCount = existingSeats;

            var snooty = await env.CreateMemberAsync();
            snooty.BillingEmail = "snooty.mcdoots@aseriousbusiness.com";
            await env.Db.SaveChangesAsync();

            var subscription = new Subscription
            {
                CustomerId = "STRIPECUSTOMERID123",
                Id = "STRIPESUBSCRIPTIONID123",
                Customer = new()
                {
                    Id = "STRIPECUSTOMERID123",
                    Name = "Snooty McDoots",
                    Email = "snooty.mcdoots@aseriousbusiness.com",
                },
                Items = new()
                {
                    Data = new List<SubscriptionItem>()
                    {
                        CreatePlanSubscriptionItem(null, purchasedSeats),
                    }
                }
            };

            var billing = env.Activate<BillingService>();

            await billing.UpdateSubscriptionAsync(subscription);

            Assert.Equal("STRIPESUBSCRIPTIONID123", organization.StripeSubscriptionId);
            Assert.Equal("STRIPECUSTOMERID123", organization.StripeCustomerId);
            Assert.Equal(organizationPlan, organization.PlanType);
            Assert.Equal(purchasedSeats, organization.PurchasedSeatCount);

            AssertBillingEvent(
                await env.AuditLog.GetMostRecentLogEntry(organization),
                snooty.User,
                organization.PlanType,
                $"Changed from the {organizationPlan} plan to the {organizationPlan} plan.",
                "STRIPECUSTOMERID123",
                "STRIPESUBSCRIPTIONID123",
                "snooty.mcdoots@aseriousbusiness.com",
                "Snooty McDoots");
        }

        [Theory]
        [InlineData(PlanType.Team)]
        [InlineData(PlanType.Business)]
        [InlineData(PlanType.FoundingCustomer)]
        public async Task SetsPlanAndSubscriptionForOrganization(PlanType plan)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.StripeCustomerId = "STRIPECUSTOMERID123";
            var snooty = await env.CreateMemberAsync();
            snooty.BillingEmail = "snooty.mcdoots@aseriousbusiness.com";
            await env.Db.SaveChangesAsync();
            var subscription = new Subscription
            {
                CustomerId = "STRIPECUSTOMERID123",
                Id = "STRIPESUBSCRIPTIONID123",
                Customer = new()
                {
                    Id = "STRIPECUSTOMERID123",
                    Name = "Snooty McDoots",
                    Email = "snooty.mcdoots@aseriousbusiness.com",
                },
                Items = new()
                {
                    Data = new List<SubscriptionItem>()
                    {
                        CreatePlanSubscriptionItem(plan),
                    }
                }
            };

            var billing = env.Activate<BillingService>();

            await billing.UpdateSubscriptionAsync(subscription);

            Assert.Equal("STRIPESUBSCRIPTIONID123", organization.StripeSubscriptionId);
            Assert.Equal("STRIPECUSTOMERID123", organization.StripeCustomerId);
            Assert.Equal(plan, organization.PlanType);

            AssertBillingEvent(
                await env.AuditLog.GetMostRecentLogEntry(organization),
                snooty.User,
                plan,
                $"Changed from the {PlanType.Unlimited} plan to the {plan} plan.",
                "STRIPECUSTOMERID123",
                "STRIPESUBSCRIPTIONID123",
                "snooty.mcdoots@aseriousbusiness.com",
                "Snooty McDoots");
        }
    }

    public class TheCancelCustomerSubscriptionAsyncMethod
    {
        [Fact]
        public async Task CancelsSubscriptionForOrganization()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.PlanType = PlanType.Business;
            organization.StripeCustomerId = "STRIPECUSTOMERID123";
            var snooty = await env.CreateMemberAsync();
            snooty.BillingEmail = "snooty.mcdoots@aseriousbusiness.com";
            await env.Db.SaveChangesAsync();

            var subscription = new Subscription
            {
                CustomerId = "STRIPECUSTOMERID123",
                Id = "STRIPESUBSCRIPTIONID123",
                Customer = new()
                {
                    Id = "STRIPECUSTOMERID123",
                    Name = "Snooty McDoots",
                    Email = "snooty.mcdoots@aseriousbusiness.com",
                },
            };

            var billing = env.Activate<BillingService>();

            await billing.CancelSubscriptionAsync(subscription);

            Assert.Null(organization.StripeCustomerId);
            Assert.Null(organization.StripeSubscriptionId);
            Assert.Equal(PlanType.Free, organization.PlanType);

            AssertBillingEvent(
                await env.AuditLog.GetMostRecentLogEntry(organization),
                snooty.User,
                PlanType.Free,
                "Canceled Business plan.",
                "STRIPECUSTOMERID123",
                "STRIPESUBSCRIPTIONID123",
                "snooty.mcdoots@aseriousbusiness.com",
                "Snooty McDoots");
        }
    }

    public class TheCompleteCheckoutAsyncMethod
    {
        [Theory]
        [InlineData(PlanType.Team)]
        [InlineData(PlanType.Business)]
        [InlineData(PlanType.FoundingCustomer)]
        public async Task AppliesRelevantPlanToOrganization(PlanType plan)
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            organization.PlanType = PlanType.Free;
            await env.Db.SaveChangesAsync();

            var session = new Session()
            {
                Metadata = new BillingContext(organization, env.TestData.Member).ToMetadata(),
            };

            var subscription = new Subscription()
            {
                CustomerId = "STRIPECUSTOMERID123",
                Id = "STRIPESUBSCRIPTIONID123",
                Customer = new()
                {
                    Id = "STRIPECUSTOMERID123",
                    Name = "Snooty McDoots",
                    Email = "snooty.mcdoots@aseriousbusiness.com",
                },
                Items = new()
                {
                    Data = new List<SubscriptionItem>()
                    {
                        CreatePlanSubscriptionItem(plan),
                    }
                },
            };

            var billing = env.Activate<BillingService>();

            await billing.CompleteCheckoutAsync(session, subscription);

            await env.ReloadAsync(env.TestData.Member, organization);
            Assert.Equal("snooty.mcdoots@aseriousbusiness.com", env.TestData.Member.BillingEmail);
            Assert.Equal(plan, organization.PlanType);
            Assert.Equal("STRIPESUBSCRIPTIONID123", organization.StripeSubscriptionId);
            Assert.Equal("STRIPECUSTOMERID123", organization.StripeCustomerId);

            AssertBillingEvent(
                await env.AuditLog.GetMostRecentLogEntry(organization),
                env.TestData.Member.User,
                plan,
                $"Purchased a {plan} plan with 3 agents.",
                "STRIPECUSTOMERID123",
                "STRIPESUBSCRIPTIONID123",
                "snooty.mcdoots@aseriousbusiness.com",
                "Snooty McDoots");

            env.AnalyticsClient.AssertTracked(
                "Subscription Started",
                AnalyticsFeature.Subscriptions,
                env.TestData.Member);
        }
    }

    static SubscriptionItem CreatePlanSubscriptionItem(PlanType? plan, int quantity = 3)
    {
        var metadata = new Dictionary<string, string>();
        if (plan != null)
        {
            metadata.Add("PlanType", plan.Value.ToString());
        }
        return new()
        {
            Quantity = quantity,
            Price = new()
            {
                Product = new()
                {
                    Metadata = metadata,
                }
            }
        };
    }

    static void AssertBillingEvent(AuditEventBase? evt, User expectedActor, PlanType expectedPlan,
        string expectedDescription,
        string expectedCustomerId, string expectedSubscriptionId, string expectedBillingEmail,
        string expectedBillingName)
    {
        Assert.NotNull(evt);
        Assert.Equal(expectedActor.Id, evt.Actor.Id);
        var billingEvent = Assert.IsType<BillingEvent>(evt);
        Assert.Equal(expectedPlan, billingEvent.PlanType);
        Assert.Equal(expectedDescription, billingEvent.Description);
        Assert.Equal(expectedCustomerId, billingEvent.CustomerId);
        Assert.Equal(expectedSubscriptionId, billingEvent.SubscriptionId);
        Assert.Equal(expectedBillingEmail, billingEvent.BillingEmail);
        Assert.Equal(expectedBillingName, billingEvent.BillingName);
    }
}
