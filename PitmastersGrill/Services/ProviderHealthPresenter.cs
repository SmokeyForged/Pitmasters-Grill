using PitmastersGrill.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PitmastersGrill.Services
{
    public sealed class ProviderHealthPresenter
    {
        public void ApplySnapshots(
            ObservableCollection<ProviderHealthSnapshot> target,
            IEnumerable<ProviderHealthSnapshot> snapshots)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            target.Clear();
            foreach (var snapshot in snapshots)
            {
                target.Add(snapshot);
            }
        }
    }
}
