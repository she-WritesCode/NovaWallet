namespace NovaWallet.Domain.Exceptions;

public static class CurrencyFormatter
{
    public static string Format(long amountKobo, string currency = "NGN")
    {
        var majorAmount = amountKobo / 100.0;
        var normalized = (currency ?? "NGN").ToUpperInvariant();
        var symbol = normalized switch
        {
            "NGN" => "₦",
            "USD" => "$",
            "GBP" => "£",
            "EUR" => "€",
            _ => $"{normalized} "
        };

        return $"{symbol}{majorAmount:N2}";
    }
}

public abstract class WalletDomainException : Exception
{
    public abstract string ErrorCode { get; }
    public abstract int StatusCode { get; }

    protected WalletDomainException(string message) : base(message) { }
}

public class WalletNotFoundException : WalletDomainException
{
    public override string ErrorCode => "WALLET_NOT_FOUND";
    public override int StatusCode => 404;

    public Guid? WalletId { get; }
    public string? CustomerId { get; }

    public WalletNotFoundException(Guid walletId)
        : base("The requested wallet could not be found. Please verify the account details.")
    {
        WalletId = walletId;
    }

    public WalletNotFoundException(string customerId)
        : base("No active wallet was found for this account.")
    {
        CustomerId = customerId;
    }
}

public class WalletFrozenException : WalletDomainException
{
    public override string ErrorCode => "WALLET_FROZEN";
    public override int StatusCode => 422;

    public Guid WalletId { get; }

    public WalletFrozenException(Guid walletId)
        : base("This wallet is temporarily suspended for security. Please contact FirstBank customer care for assistance.")
    {
        WalletId = walletId;
    }
}

public class InsufficientFundsException : WalletDomainException
{
    public override string ErrorCode => "INSUFFICIENT_FUNDS";
    public override int StatusCode => 422;

    public Guid WalletId { get; }
    public long RequestedKobo { get; }
    public long AvailableKobo { get; }
    public string Currency { get; }

    public InsufficientFundsException(Guid walletId, long requestedKobo, long availableKobo, string currency = "NGN")
        : base($"Insufficient funds. Your available balance is {CurrencyFormatter.Format(availableKobo, currency)}, but you tried to transfer {CurrencyFormatter.Format(requestedKobo, currency)}.")
    {
        WalletId = walletId;
        RequestedKobo = requestedKobo;
        AvailableKobo = availableKobo;
        Currency = currency;
    }
}

public class DailyLimitExceededException : WalletDomainException
{
    public override string ErrorCode => "DAILY_LIMIT_EXCEEDED";
    public override int StatusCode => 422;

    public Guid WalletId { get; }
    public long AttemptedKobo { get; }
    public long CurrentOutboundKobo { get; }
    public long DailyLimitKobo { get; }
    public string Currency { get; }

    public DailyLimitExceededException(Guid walletId, long attemptedKobo, long currentOutboundKobo, long dailyLimitKobo, string currency = "NGN")
        : base($"Daily transfer limit reached. You can only send up to {CurrencyFormatter.Format(dailyLimitKobo, currency)} per day. You have already sent {CurrencyFormatter.Format(currentOutboundKobo, currency)} today. Your limit resets at midnight WAT.")
    {
        WalletId = walletId;
        AttemptedKobo = attemptedKobo;
        CurrentOutboundKobo = currentOutboundKobo;
        DailyLimitKobo = dailyLimitKobo;
        Currency = currency;
    }
}

public class InvalidAmountException : WalletDomainException
{
    public override string ErrorCode => "INVALID_AMOUNT";
    public override int StatusCode => 400;

    public long AmountKobo { get; }
    public string Currency { get; }

    public InvalidAmountException(long amountKobo, string currency = "NGN")
        : base($"Please enter a valid transfer amount greater than {CurrencyFormatter.Format(0, currency)}.")
    {
        AmountKobo = amountKobo;
        Currency = currency;
    }
}

public class CurrencyMismatchException : WalletDomainException
{
    public override string ErrorCode => "CURRENCY_MISMATCH";
    public override int StatusCode => 400;

    public string SourceCurrency { get; }
    public string DestinationCurrency { get; }

    public CurrencyMismatchException(string sourceCurrency, string destinationCurrency)
        : base($"Cross-currency transfers are not supported. Source is {sourceCurrency.ToUpperInvariant()}, but destination is {destinationCurrency.ToUpperInvariant()}.")
    {
        SourceCurrency = sourceCurrency;
        DestinationCurrency = destinationCurrency;
    }
}

public class SameWalletTransferException : WalletDomainException
{
    public override string ErrorCode => "SELF_TRANSFER_NOT_ALLOWED";
    public override int StatusCode => 400;

    public Guid WalletId { get; }

    public SameWalletTransferException(Guid walletId)
        : base("You cannot transfer funds to the same wallet. Please select a different recipient account.")
    {
        WalletId = walletId;
    }
}

public class DuplicateWalletException : WalletDomainException
{
    public override string ErrorCode => "DUPLICATE_WALLET";
    public override int StatusCode => 409;

    public string CustomerId { get; }
    public string Currency { get; }

    public DuplicateWalletException(string customerId, string currency)
        : base($"A {currency.ToUpperInvariant()} wallet already exists for this account.")
    {
        CustomerId = customerId;
        Currency = currency;
    }
}
