namespace FleetControl.Application.DTOs;

public record UploadPhotoDto(Guid VehicleId, bool IsPrimary);

public record VehiclePhotoDto(Guid Id, Guid VehicleId, string Url, bool IsPrimary);
