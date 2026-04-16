using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public class PilotRegistryReadService
    {
        private readonly PilotRegistryDayRepository _pilotRegistryDayRepository;

        public PilotRegistryReadService(PilotRegistryDayRepository pilotRegistryDayRepository)
        {
            _pilotRegistryDayRepository = pilotRegistryDayRepository;
        }

        public Dictionary<string, PilotRegistryAggregate> GetAggregatesByCharacterIds(
            IEnumerable<string> characterIds)
        {
            return _pilotRegistryDayRepository.GetAggregatesByCharacterIds(characterIds);
        }
    }
}