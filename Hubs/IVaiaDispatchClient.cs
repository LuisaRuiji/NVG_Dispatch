using NVGInventory.Hubs.Events;

namespace NVGInventory.Hubs;

public interface IVaiaDispatchClient
{
    Task TripStatusChanged(TripStatusChangedEvent e);
    Task DocumentUploaded(DocumentUploadedEvent e);
    Task DocumentVerified(DocumentVerifiedEvent e);
    Task RecommendationGenerated(RecommendationGeneratedEvent e);
    Task DriverLocationUpdated(DriverLocationUpdatedEvent e);
    Task ShipmentRequestSubmitted(ShipmentRequestSubmittedEvent e);
}
