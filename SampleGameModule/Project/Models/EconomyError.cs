namespace SampleGameModule.Models;

/// <summary>Economy-specific errors, with the status codes the sample clients key on.</summary>
public class EconomyError : ModuleError
{
    // Matches Economy's "purchase costs not met" code, referenced by
    // Virtual Shop/Scripts/EconomyManager.cs and Starter Pack/Scripts/CloudCodeManager.cs.
    public const int PurchaseCostsNotMetStatus = 10504;

    // Matches the k_EconomyValidationExceptionStatusCode the samples already handle.
    public const int ValidationStatus = 1007;
    public const int ConflictStatus = 409;

    public EconomyError(int status, string name, string message)
        : base(status, name, message)
    {
    }

    public static EconomyError PurchaseCostsNotMet(string purchaseId)
    {
        return new EconomyError(PurchaseCostsNotMetStatus, "EconomyPurchaseCostsNotMet",
            $"Purchase costs not met for \"{purchaseId}\".");
    }

    public static EconomyError UnknownPurchase(string purchaseId)
    {
        return new EconomyError(PurchaseCostsNotMetStatus, "EconomyPurchaseCostsNotMet",
            $"Unknown virtual purchase id \"{purchaseId}\".");
    }

    public static EconomyError NegativeBalance(string currencyId)
    {
        return new EconomyError(ValidationStatus, "EconomyValidation",
            $"Balance of \"{currencyId}\" cannot go negative.");
    }

    public static EconomyError NegativeCount(string itemId)
    {
        return new EconomyError(ValidationStatus, "EconomyValidation",
            $"Inventory count of \"{itemId}\" cannot go negative.");
    }

    public static EconomyError WriteConflict()
    {
        return new EconomyError(ConflictStatus, "CloudSaveConflict",
            "Cloud Save write conflict persisted after retries; please try again.");
    }
}
