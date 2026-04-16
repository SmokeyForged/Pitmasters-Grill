using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PitmastersLittleGrill.Models
{
    public class PilotBoardRow : INotifyPropertyChanged
    {
        private string _characterName = "";
        private string _characterId = "";
        private string _allianceName = "";
        private string _allianceTicker = "";
        private string _corpName = "";
        private string _corpTicker = "";
        private int? _killCount;
        private int? _lossCount;
        private double? _avgAttackersWhenAttacking;
        private string _lastPublicCynoCapableHull = "";
        private string _lastShipSeenName = "";
        private string _lastShipSeenAtUtc = "";
        private string _lastShipSeenDateDisplay = "";
        private bool _knownCynoOverride;
        private bool _baitOverride;
        private bool _isResolved;
        private string _resolverConfidence = "";
        private string _resolvedAtUtc = "";
        private string _affiliationCheckedAtUtc = "";
        private EnrichmentStageState _identityStage = EnrichmentStageState.NotStarted;
        private EnrichmentStageState _affiliationStage = EnrichmentStageState.NotStarted;
        private EnrichmentStageState _statsStage = EnrichmentStageState.NotStarted;
        private string _identityStatusDetail = "";
        private string _affiliationStatusDetail = "";
        private string _statsStatusDetail = "";
        private DateTime? _identityRetryAtUtc;
        private DateTime? _affiliationRetryAtUtc;
        private DateTime? _statsRetryAtUtc;
        private DateTime? _nextRetryAtUtc;
        private string _lastThrottleProvider = "";

        public string CharacterName
        {
            get => _characterName;
            set => SetField(ref _characterName, value);
        }

        public string CharacterId
        {
            get => _characterId;
            set => SetField(ref _characterId, value);
        }

        public string AllianceName
        {
            get => _allianceName;
            set => SetField(ref _allianceName, value);
        }

        public string AllianceTicker
        {
            get => _allianceTicker;
            set => SetField(ref _allianceTicker, value);
        }

        public string CorpName
        {
            get => _corpName;
            set => SetField(ref _corpName, value);
        }

        public string CorpTicker
        {
            get => _corpTicker;
            set => SetField(ref _corpTicker, value);
        }

        public int? KillCount
        {
            get => _killCount;
            set => SetField(ref _killCount, value);
        }

        public int? LossCount
        {
            get => _lossCount;
            set => SetField(ref _lossCount, value);
        }

        public double? AvgAttackersWhenAttacking
        {
            get => _avgAttackersWhenAttacking;
            set => SetField(ref _avgAttackersWhenAttacking, value);
        }

        public string LastPublicCynoCapableHull
        {
            get => _lastPublicCynoCapableHull;
            set => SetField(ref _lastPublicCynoCapableHull, value);
        }

        public string LastShipSeenName
        {
            get => _lastShipSeenName;
            set => SetField(ref _lastShipSeenName, value);
        }

        public string LastShipSeenAtUtc
        {
            get => _lastShipSeenAtUtc;
            set => SetField(ref _lastShipSeenAtUtc, value);
        }

        public string LastShipSeenDateDisplay
        {
            get => _lastShipSeenDateDisplay;
            set => SetField(ref _lastShipSeenDateDisplay, value);
        }

        public bool KnownCynoOverride
        {
            get => _knownCynoOverride;
            set => SetField(ref _knownCynoOverride, value);
        }

        public bool BaitOverride
        {
            get => _baitOverride;
            set => SetField(ref _baitOverride, value);
        }

        public bool IsResolved
        {
            get => _isResolved;
            set => SetField(ref _isResolved, value);
        }

        public string ResolverConfidence
        {
            get => _resolverConfidence;
            set => SetField(ref _resolverConfidence, value);
        }

        public string ResolvedAtUtc
        {
            get => _resolvedAtUtc;
            set => SetField(ref _resolvedAtUtc, value);
        }

        public string AffiliationCheckedAtUtc
        {
            get => _affiliationCheckedAtUtc;
            set => SetField(ref _affiliationCheckedAtUtc, value);
        }

        public EnrichmentStageState IdentityStage
        {
            get => _identityStage;
            set => SetField(ref _identityStage, value);
        }

        public EnrichmentStageState AffiliationStage
        {
            get => _affiliationStage;
            set => SetField(ref _affiliationStage, value);
        }

        public EnrichmentStageState StatsStage
        {
            get => _statsStage;
            set => SetField(ref _statsStage, value);
        }

        public string IdentityStatusDetail
        {
            get => _identityStatusDetail;
            set => SetField(ref _identityStatusDetail, value);
        }

        public string AffiliationStatusDetail
        {
            get => _affiliationStatusDetail;
            set => SetField(ref _affiliationStatusDetail, value);
        }

        public string StatsStatusDetail
        {
            get => _statsStatusDetail;
            set => SetField(ref _statsStatusDetail, value);
        }

        public DateTime? IdentityRetryAtUtc
        {
            get => _identityRetryAtUtc;
            set => SetField(ref _identityRetryAtUtc, value);
        }

        public DateTime? AffiliationRetryAtUtc
        {
            get => _affiliationRetryAtUtc;
            set => SetField(ref _affiliationRetryAtUtc, value);
        }

        public DateTime? StatsRetryAtUtc
        {
            get => _statsRetryAtUtc;
            set => SetField(ref _statsRetryAtUtc, value);
        }

        public DateTime? NextRetryAtUtc
        {
            get => _nextRetryAtUtc;
            set => SetField(ref _nextRetryAtUtc, value);
        }

        public string LastThrottleProvider
        {
            get => _lastThrottleProvider;
            set => SetField(ref _lastThrottleProvider, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}