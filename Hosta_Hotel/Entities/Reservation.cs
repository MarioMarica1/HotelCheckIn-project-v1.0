namespace Hosta_Hotel.Entities;

public record Reservation
{
    public string ClientUsername { get; set; } = string.Empty;
    public int RoomNumber { get; init; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCheckedIn { get; set; } = false;
    public bool IsCheckedOut { get; set; } = false;
}