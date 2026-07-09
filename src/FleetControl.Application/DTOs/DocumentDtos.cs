using FleetControl.Domain.Enums;

namespace FleetControl.Application.DTOs;

public record UploadDocumentDto(
    Guid VehicleId,
    DocumentType DocumentType,
    DateOnly IssueDate,
    DateOnly ExpirationDate);

public record UpdateDocumentDatesDto(
    DateOnly IssueDate,
    DateOnly ExpirationDate);

public record DocumentDto(
    Guid Id,
    Guid VehicleId,
    DocumentType DocumentType,
    DateOnly IssueDate,
    DateOnly ExpirationDate,
    string FileHashSha256,
    AlertStatus Status,
    int DaysUntilExpiration,
    bool IsCurrent);
