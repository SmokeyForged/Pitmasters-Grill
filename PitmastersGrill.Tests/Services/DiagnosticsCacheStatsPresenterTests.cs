using PitmastersGrill.Models;
using PitmastersGrill.Services;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class DiagnosticsCacheStatsPresenterTests
    {
        [Fact]
        public void BuildFailureText_UsesExistingUiFailureMessageShape()
        {
            var presenter = new DiagnosticsCacheStatsPresenter();

            var result = presenter.BuildFailureText(new System.InvalidOperationException("boom"));

            Assert.Equal("Cache stats failed: boom", result);
        }

        [Fact]
        public void BuildStatsText_DelegatesToCacheStatsFormatter()
        {
            var presenter = new DiagnosticsCacheStatsPresenter();
            var stats = new CacheStatsSnapshot
            {
                DatabasePathDisplay = "db.sqlite",
                Status = "Cache stats loaded."
            };

            var result = presenter.BuildStatsText(stats);

            Assert.Contains("Database: db.sqlite", result);
            Assert.Contains("Status: Cache stats loaded.", result);
        }
    }
}
