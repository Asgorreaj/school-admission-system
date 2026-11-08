namespace SchoolAdmission.Api.Services; // service namespace

public class AppException(int status, string message) : Exception(message) // business error (4xx)
{
    public int Status { get; } = status; // HTTP status
}