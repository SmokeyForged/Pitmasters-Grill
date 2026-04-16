using System;

namespace PitmastersGrill.Models
{
    public enum ProviderOutcomeKind
    {
        Success,
        NotFound,
        Throttled,
        TemporaryFailure,
        PermanentFailure,
        Skipped
    }

    public class ProviderOutcome<T>
    {
        public ProviderOutcomeKind Kind { get; set; }
        public T? Value { get; set; }
        public string ProviderName { get; set; } = "";
        public string Detail { get; set; } = "";
        public DateTime? RetryAfterUtc { get; set; }

        public bool IsRetryable =>
            Kind == ProviderOutcomeKind.Throttled ||
            Kind == ProviderOutcomeKind.TemporaryFailure;

        public static ProviderOutcome<T> Success(T value, string providerName, string detail = "")
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.Success,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = null
            };
        }

        public static ProviderOutcome<T> NotFound(string providerName, string detail = "", T? value = default)
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.NotFound,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = null
            };
        }

        public static ProviderOutcome<T> Throttled(string providerName, string detail = "", DateTime? retryAfterUtc = null, T? value = default)
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.Throttled,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = retryAfterUtc
            };
        }

        public static ProviderOutcome<T> TemporaryFailure(string providerName, string detail = "", DateTime? retryAfterUtc = null, T? value = default)
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.TemporaryFailure,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = retryAfterUtc
            };
        }

        public static ProviderOutcome<T> PermanentFailure(string providerName, string detail = "", T? value = default)
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.PermanentFailure,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = null
            };
        }

        public static ProviderOutcome<T> Skipped(string providerName, string detail = "", T? value = default)
        {
            return new ProviderOutcome<T>
            {
                Kind = ProviderOutcomeKind.Skipped,
                Value = value,
                ProviderName = providerName,
                Detail = detail,
                RetryAfterUtc = null
            };
        }
    }
}