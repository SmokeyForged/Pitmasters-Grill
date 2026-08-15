using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PitmastersGrill.Services
{
    internal sealed class R2Z2SequenceClient
    {
        private const string CurrentSequenceUrl = "https://r2z2.zkillboard.com/ephemeral/sequence.json";
        private const string SequenceFileUrlFormat = "https://r2z2.zkillboard.com/ephemeral/{0}.json";
        private static readonly TimeSpan DefaultSuccessPacingDelay = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan CaughtUpBaseDelay = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan CaughtUpJitterMaxDelay = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan RateLimitBaseDelay = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan RateLimitJitterMaxDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan RateLimitMaxDelay = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan DefaultErrorBackoffDelay = TimeSpan.FromSeconds(15);

        private readonly HttpClient _httpClient;
        private readonly Func<double> _jitterFractionProvider;

        public R2Z2SequenceClient()
            : this(CreateHttpClient(), () => Random.Shared.NextDouble())
        {
        }

        internal R2Z2SequenceClient(
            HttpClient httpClient,
            Func<double>? jitterFractionProvider = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _jitterFractionProvider = jitterFractionProvider ?? (() => Random.Shared.NextDouble());
        }

        public TimeSpan SuccessPacingDelay => DefaultSuccessPacingDelay;

        public TimeSpan ErrorBackoffDelay => DefaultErrorBackoffDelay;

        public async Task<long> GetCurrentSequenceIdAsync(CancellationToken cancellationToken)
        {
            AppLogger.KillmailImportDebug("R2Z2 current sequence fetch begin.");

            using var response = await _httpClient
                .GetAsync(CurrentSequenceUrl, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!TryParseCurrentSequenceId(payload, out var currentSequenceId))
            {
                throw new InvalidOperationException("Unable to parse the current R2Z2 sequence.");
            }

            AppLogger.KillmailImportDebug($"R2Z2 current sequence fetch end. currentSequence={currentSequenceId}");
            return currentSequenceId;
        }

        public async Task<R2Z2SequenceFetchResult> FetchSequenceAsync(
            long sequenceId,
            int consecutiveRateLimitCount,
            CancellationToken cancellationToken)
        {
            var sequenceUrl = string.Format(
                CultureInfo.InvariantCulture,
                SequenceFileUrlFormat,
                sequenceId);

            AppLogger.KillmailImportDebug(
                $"R2Z2 sequence fetch start. sequence={sequenceId} url={sequenceUrl}");

            using var response = await _httpClient
                .GetAsync(sequenceUrl, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new R2Z2SequenceFetchResult
                {
                    Status = R2Z2SequenceFetchStatus.NotFound,
                    RetryDelay = BuildCaughtUpDelay()
                };
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                var retryDelay = GetRateLimitDelay(
                    response,
                    consecutiveRateLimitCount,
                    out var delaySource);

                return new R2Z2SequenceFetchResult
                {
                    Status = R2Z2SequenceFetchStatus.RateLimited,
                    RetryDelay = retryDelay,
                    DelaySource = delaySource
                };
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return new R2Z2SequenceFetchResult
                {
                    Status = R2Z2SequenceFetchStatus.Forbidden
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                return new R2Z2SequenceFetchResult
                {
                    Status = R2Z2SequenceFetchStatus.Error,
                    Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}",
                    RetryDelay = DefaultErrorBackoffDelay
                };
            }

            return new R2Z2SequenceFetchResult
            {
                Status = R2Z2SequenceFetchStatus.Success,
                Content = await response.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false)
            };
        }

        internal static bool TryParseCurrentSequenceId(string payload, out long sequenceId)
        {
            sequenceId = 0;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Number && root.TryGetInt64(out sequenceId))
            {
                return true;
            }

            if (TryReadLong(root, "sequence", out sequenceId) ||
                TryReadLong(root, "sequence_id", out sequenceId))
            {
                return true;
            }

            return false;
        }

        private TimeSpan BuildCaughtUpDelay()
        {
            return CaughtUpBaseDelay + BuildJitter(CaughtUpJitterMaxDelay);
        }

        private TimeSpan GetRateLimitDelay(
            HttpResponseMessage response,
            int consecutiveRateLimitCount,
            out string delaySource)
        {
            if (TryGetRetryAfterDelay(response, out var retryAfterDelay))
            {
                delaySource = "retry-after";
                return retryAfterDelay + BuildJitter(RateLimitJitterMaxDelay);
            }

            var multiplier = Math.Max(0, consecutiveRateLimitCount - 1);
            var exponentialSeconds = RateLimitBaseDelay.TotalSeconds * Math.Pow(2, multiplier);
            var cappedDelay = TimeSpan.FromSeconds(
                Math.Min(exponentialSeconds, RateLimitMaxDelay.TotalSeconds));
            delaySource = "exponential";
            return cappedDelay + BuildJitter(RateLimitJitterMaxDelay);
        }

        private static bool TryGetRetryAfterDelay(
            HttpResponseMessage response,
            out TimeSpan retryDelay)
        {
            retryDelay = TimeSpan.Zero;

            var retryAfter = response?.Headers?.RetryAfter;
            if (retryAfter == null)
            {
                return false;
            }

            if (retryAfter.Delta.HasValue && retryAfter.Delta.Value > TimeSpan.Zero)
            {
                retryDelay = retryAfter.Delta.Value;
                return true;
            }

            if (retryAfter.Date.HasValue)
            {
                var delay = retryAfter.Date.Value.UtcDateTime - DateTime.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    retryDelay = delay;
                    return true;
                }
            }

            return false;
        }

        private TimeSpan BuildJitter(TimeSpan maxJitter)
        {
            if (maxJitter <= TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            var fraction = Math.Clamp(_jitterFractionProvider(), 0, 1);
            return TimeSpan.FromMilliseconds(fraction * maxJitter.TotalMilliseconds);
        }

        private static bool TryReadLong(
            JsonElement element,
            string propertyName,
            out long value)
        {
            value = 0;

            if (!element.TryGetProperty(propertyName, out var property))
            {
                return false;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt64(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(
                    property.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            return false;
        }

        private static HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                AppHttpDefaults.GenericUserAgent);
            return httpClient;
        }
    }

    internal enum R2Z2SequenceFetchStatus
    {
        Success,
        NotFound,
        RateLimited,
        Forbidden,
        Error
    }

    internal sealed class R2Z2SequenceFetchResult
    {
        public R2Z2SequenceFetchStatus Status { get; set; }
        public string Content { get; set; } = "";
        public TimeSpan RetryDelay { get; set; }
        public string DelaySource { get; set; } = "";
        public string Error { get; set; } = "";
    }
}
