using System;

namespace PitmastersGrill.Services
{
    public sealed class BoardStatusPresenter
    {
        private readonly TimeProvider _timeProvider;

        public BoardStatusPresenter(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        public string BuildLastRefreshedText()
        {
            var localNow = _timeProvider.GetLocalNow();
            return $"Last Refreshed: {localNow:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
