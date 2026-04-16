using PitmastersGrill.Models;
using PitmastersGrill.Persistence;
using System.Collections.Generic;

namespace PitmastersGrill.Services
{
    public class FleetObservationReadService
    {
        private readonly PilotFleetObservationDayRepository _pilotFleetObservationDayRepository;

        public FleetObservationReadService(PilotFleetObservationDayRepository pilotFleetObservationDayRepository)
        {
            _pilotFleetObservationDayRepository = pilotFleetObservationDayRepository;
        }

        public Dictionary<string, PilotFleetObservationAggregate> GetAggregatesByCharacterIds(
            IEnumerable<string> characterIds)
        {
            return _pilotFleetObservationDayRepository.GetAggregatesByCharacterIds(characterIds);
        }
    }
}