namespace Hosta_Hotel.Entities;

public record Room
{
    public int RoomNumber { get; init; }
    public string Status { get; set; } = "Free"; 
    public string RoomType { get; set; } = "Single";
    public int PricePerNight { get; set; }
}