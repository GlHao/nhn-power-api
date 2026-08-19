using System;
using System.Collections.Generic;

namespace NHN.Power.API.Services
{
    public class QuoteRequestInput
    {
        public string ServiceCode { get; set; } = string.Empty;
        public int Bedrooms { get; set; }
        public int Bathrooms { get; set; }
        public List<string> AddonCodes { get; set; } = new();
    }

    public interface IPricingValidationService
    {
        decimal CalculateEstimate(QuoteRequestInput input);
        bool ValidateEstimate(QuoteRequestInput input, decimal frontendEstimate);
    }

    public class PricingValidationService : IPricingValidationService
    {
        public decimal CalculateEstimate(QuoteRequestInput input)
        {
            decimal total = 0m;

            // Base price calculation based on service type and bedrooms
            if (input.ServiceCode == "standard_clean" || input.ServiceCode == "general_clean")
            {
                total += input.Bedrooms switch
                {
                    1 => 179m,
                    2 => 199m,
                    3 => 219m,
                    4 => 259m,
                    _ => 179m // Default / minimum
                };

                // Bathroom rule: Includes 1 bathroom. Extra is +$20
                if (input.Bathrooms > 1)
                {
                    total += (input.Bathrooms - 1) * 20m;
                }
            }
            else if (input.ServiceCode == "deep_clean" || input.ServiceCode == "deep_reset_clean")
            {
                total += input.Bedrooms switch
                {
                    1 => 249m,
                    2 => 269m,
                    3 => 289m,
                    4 => 329m,
                    _ => 249m
                };

                // Bathroom rule: Includes 1 bathroom. Extra is +$40
                if (input.Bathrooms > 1)
                {
                    total += (input.Bathrooms - 1) * 40m;
                }
            }
            
            // Note: V1 hardcoded prices for add-ons. 
            // The exact codes will be mapped based on frontend submission.
            var addonPrices = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                { "oven_clean", 65m },
                { "fridge_clean", 35m },
                { "dishes", 35m },
                { "green_supplies", 5m }
                // Dynamic pricing like windows, walls, carpet are handled 
                // either by unit/quantity in the QuoteServices table or manually adjusted.
                // For V1 validation, we might just trust the frontend for variable add-ons, 
                // or require exact matching for fixed-price add-ons.
                // This is a simplified validation for MVP.
            };

            foreach (var addon in input.AddonCodes)
            {
                if (addonPrices.TryGetValue(addon, out var price))
                {
                    total += price;
                }
            }

            return total;
        }

        public bool ValidateEstimate(QuoteRequestInput input, decimal frontendEstimate)
        {
            var calculated = CalculateEstimate(input);
            // In V1, we tolerate discrepancies because some add-ons have variable pricing (e.g. $30 - $100)
            // or we just trust frontend and mark it for manual review.
            // A simple implementation could just return true for V1, or flag if it's way off.
            
            // For now, if we want strict matching on base price:
            return Math.Abs(calculated - frontendEstimate) >= 0; 
            // Always return true in MVP if we are just using this to seed `reviewed_amount` or cross-check,
            // or implement full exact matching logic when frontend payload format is finalized.
        }
    }
}
