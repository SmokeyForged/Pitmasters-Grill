using PitmastersLittleGrill.Models;
using PitmastersLittleGrill.Persistence;
using System.Collections.Generic;

namespace PitmastersLittleGrill.Services
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