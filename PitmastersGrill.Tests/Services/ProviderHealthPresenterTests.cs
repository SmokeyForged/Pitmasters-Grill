using PitmastersGrill.Models;
using PitmastersGrill.Services;
using System.Collections.ObjectModel;
using Xunit;

namespace PitmastersGrill.Tests.Services
{
    public sealed class ProviderHealthPresenterTests
    {
        [Fact]
        public void ApplySnapshots_ReplacesExistingRowsWithIncomingSnapshots()
        {
            var presenter = new ProviderHealthPresenter();
            var target = new ObservableCollection<ProviderHealthSnapshot>
            {
                new() { ProviderName = "old" }
            };
            var snapshots = new[]
            {
                new ProviderHealthSnapshot { ProviderName = "alpha" },
                new ProviderHealthSnapshot { ProviderName = "beta" }
            };

            presenter.ApplySnapshots(target, snapshots);

            Assert.Collection(
                target,
                item => Assert.Equal("alpha", item.ProviderName),
                item => Assert.Equal("beta", item.ProviderName));
        }
    }
}
