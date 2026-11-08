namespace SchoolAdmission.Api.Models; // DTO namespace

public record CreateClassRequest(string? Name, int TotalSeats); // POST /classes input
public record ClassDto(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats); // class output
public record HoldDto(string HoldId, string ClassId, string ClassName, string StudentId, string Status, DateTime ExpiresAt, DateTime ServerTime); // hold output (ServerTime frontend countdown-er jonno)