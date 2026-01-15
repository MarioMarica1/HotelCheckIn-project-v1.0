using Microsoft.Extensions.Logging;
using Hosta_Hotel.Entities;
using Hosta_Hotel.Infrastructure;

namespace Hosta_Hotel.TheBrain;

public class Hotel
{
    // --- STARE INTERNĂ (Datele) ---
    public List<Room> Rooms { get; private set; }
    public List<Client> Clients { get; private set; }
    public List<Administrator> Admins { get; private set; }
    public List<Cleaner> Cleaners { get; private set; }
    public List<Reservation> Reservations { get; private set; }

    // Reguli & Timp
    public TimeSpan CheckInStart { get; private set; }
    public TimeSpan CheckOutLimit { get; private set; }
    
    // DATA SIMULATĂ A HOTELULUI
    public DateTime CurrentDate { get; private set; }

    // Dependențe
    private readonly ILogger<Hotel> _logger;
    private readonly PersistenceManager _persistence;
    
    public Hotel(ILogger<Hotel> logger, PersistenceManager persistence)
    {
        _logger = logger;
        _persistence = persistence;

        var data = _persistence.LoadData();
        Rooms = data.Rooms ?? new List<Room>(); 
        Clients = data.Clients ?? new List<Client>();
        Admins = data.Admins ?? new List<Administrator>();
        Cleaners = data.Cleaners ?? new List<Cleaner>();
        Reservations = data.Reservations ?? new List<Reservation>();
        
        CheckInStart = data.CheckInStart == default ? new TimeSpan(14, 0, 0) : data.CheckInStart;
        CheckOutLimit = data.CheckOutLimit == default ? new TimeSpan(11, 0, 0) : data.CheckOutLimit;

        CurrentDate = DateTime.Now.Date;
        EnsureSeedData();
    }

    public void SetSimulationDate(DateTime date)
    {
        CurrentDate = date.Date; 
        _logger.LogInformation($"SYSTEM: Data simulata a fost setata la {CurrentDate:yyyy-MM-dd}");
    }

    // ==========================================================
    // 1. METODE ADMINISTRATOR
    // ==========================================================

    public void AddRoom(int number, string type, int price)
    {
        if (Rooms.Any(r => r.RoomNumber == number))
            throw new Exception($"Camera {number} există deja.");

        var validTypes = new List<string> { "Single", "Double", "Suite" };
        if (!validTypes.Contains(type))
        {
            throw new Exception($"Tip invalid. Tipuri permise: {string.Join(", ", validTypes)}");
        }

        Rooms.Add(new Room { RoomNumber = number, RoomType = type, PricePerNight = price, Status = "Free" });
        SaveChanges();
        _logger.LogInformation($"ADMIN: Adaugat camera {number} ({type})");
    }

    public void RemoveRoom(int roomNumber)
    {
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room == null) throw new Exception("Camera nu exista.");

        Rooms.Remove(room);
        SaveChanges();
        _logger.LogInformation($"ADMIN: Sters camera {roomNumber}");
    }

    public void SetRoomStatus(int roomNumber, string newStatus)
    {
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room == null) throw new Exception("Camera nu exista.");

        room.Status = newStatus;
        SaveChanges();
        _logger.LogInformation($"ADMIN: Schimbat status camera {roomNumber} in {newStatus}");
    }

    public void AddCleaner(string firstName, string lastName, string username, string password)
    {
        if (Cleaners.Any(c => c.UsernameID == username))
            throw new Exception("Username-ul exista deja.");

        Cleaners.Add(new Cleaner { FirstName = firstName, LastName = lastName, UsernameID = username, Password = password });
        SaveChanges();
        _logger.LogInformation($"ADMIN: Angajat cleaner {username}");
    }

    public void RemoveCleaner(string username)
    {
        var cleaner = Cleaners.FirstOrDefault(c => c.UsernameID == username);
        if (cleaner != null)
        {
            Cleaners.Remove(cleaner);
            SaveChanges();
            _logger.LogInformation($"ADMIN: Concediat cleaner {username}");
        }
    }

    public void UpdateCheckInTime(TimeSpan newTime)
    {
        CheckInStart = newTime;
        SaveChanges();
        _logger.LogInformation($"ADMIN: Ora Check-in schimbata la {newTime}");
    }

    public void UpdateCheckOutTime(TimeSpan newTime)
    {
        CheckOutLimit = newTime;
        SaveChanges();
        _logger.LogInformation($"ADMIN: Ora limita Check-out schimbata la {newTime}");
    }


    public List<Reservation> GetClientReservations(string clientUsername)
    {
        return Reservations.Where(r => r.ClientUsername == clientUsername).ToList();
    }

    public void AdminDeleteClient(string clientUsername)
    {
        var client = Clients.FirstOrDefault(c => c.UsernameID == clientUsername);
        if (client == null) throw new Exception("Clientul nu exista.");

        // Verificăm dacă are rezervări active (Check-in făcut sau viitoare neanulate)
        bool hasActiveReservations = Reservations.Any(r => 
            r.ClientUsername == clientUsername && !r.IsCheckedOut);

        if (hasActiveReservations)
        {
            throw new Exception("Clientul are rezervări active. Anulati-le sau faceti Check-Out înainte de stergere.");
        }

        // Ștergem și istoricul rezervărilor (Curățăm complet urmele)
        Reservations.RemoveAll(r => r.ClientUsername == clientUsername);
        
        Clients.Remove(client);
        SaveChanges();
        _logger.LogInformation($"ADMIN: A sters contul clientului {clientUsername} și istoricul aferent.");
    }

    public void AdminCancelReservation(string clientUsername, int roomNumber)
    {
        var res = FindActiveReservation(clientUsername, roomNumber);
        res.IsCheckedOut = true; 
        
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room != null) room.Status = "Free";

        SaveChanges();
        _logger.LogInformation($"ADMIN: Anulat rezervarea clientului {clientUsername} la camera {roomNumber}");
    }

    public void AdminChangeReservationPeriod(string clientUsername, int roomNumber, DateTime newStart, DateTime newEnd)
    {
        var res = FindActiveReservation(clientUsername, roomNumber);
        if (newStart >= newEnd) throw new Exception("Data de sfarsit invalida.");
        
        res.StartDate = newStart;
        res.EndDate = newEnd;
        SaveChanges();
        _logger.LogInformation($"ADMIN: Modificat perioada pentru {clientUsername} la camera {roomNumber}");
    }

    public void AdminForceCheckIn(string clientUsername, int roomNumber)
    {
        var res = FindActiveReservation(clientUsername, roomNumber);
        res.IsCheckedIn = true;
        
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room != null) room.Status = "Occupied";

        SaveChanges();
        _logger.LogInformation($"ADMIN: Check-in fortat pentru {clientUsername} la camera {roomNumber}");
    }

    private Reservation FindActiveReservation(string username, int roomNr)
    {
        var res = Reservations.FirstOrDefault(r => 
            r.ClientUsername == username && 
            r.RoomNumber == roomNr && 
            r.IsCheckedOut == false);
        if (res == null) throw new Exception("Rezervarea activa nu a fost găsita.");
        return res;
    }

    // ==========================================================
    // 2. METODE CLIENT
    // ==========================================================

    public void MakeReservation(string clientUsername, int roomNumber, DateTime start, int days)
    {
        DateTime end = start.AddDays(days);

        if (start.Date < CurrentDate.Date) throw new Exception("Nu puteti face rezervări în trecut.");
        if (days < 1) throw new Exception("Trebuie să rezervați minim o noapte.");

        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room == null) throw new Exception("Camera nu exista.");
        
        if (start.Date == CurrentDate.Date && room.Status != "Free") 
            throw new Exception("Camera nu este libera astazi.");

        bool isOccupied = Reservations.Any(r => 
            r.RoomNumber == roomNumber &&
            !r.IsCheckedOut && 
            (start < r.EndDate && end > r.StartDate)); 

        if (isOccupied) throw new Exception("Camera este deja rezervată.");

        var res = new Reservation
        {
            ClientUsername = clientUsername,
            RoomNumber = roomNumber,
            StartDate = start,
            EndDate = end
        };
        Reservations.Add(res);

        if (start.Date == CurrentDate.Date)
        {
            room.Status = "Occupied"; 
        }

        SaveChanges();
        _logger.LogInformation($"CLIENT {clientUsername}: Rezervat camera {roomNumber} ({days} zile).");
    }

    public List<Reservation> GetReservationsForCheckIn(string clientUsername)
    {
        return Reservations.Where(r => 
            r.ClientUsername == clientUsername &&
            !r.IsCheckedIn &&
            !r.IsCheckedOut &&
            r.StartDate.Date <= CurrentDate.Date && 
            r.EndDate.Date > CurrentDate.Date
        ).ToList();
    }

    public void SelfCheckIn(string clientUsername, int roomNumber)
    {
        // Verificare Oră (Reală)
        if (DateTime.Now.TimeOfDay < CheckInStart)
            throw new Exception($"Este prea devreme. Check-in incepe la {CheckInStart}. Ora actuala: {DateTime.Now:HH:mm}");

        var res = Reservations.FirstOrDefault(r => r.ClientUsername == clientUsername && r.RoomNumber == roomNumber && !r.IsCheckedIn && !r.IsCheckedOut);
        
        if (res == null) throw new Exception("Nu aveți o rezervare validă pentru această cameră.");

        if (res.StartDate.Date > CurrentDate.Date)
            throw new Exception($"Rezervarea începe abia pe {res.StartDate:dd/MM/yyyy}. Azi e {CurrentDate:dd/MM/yyyy}.");

        res.IsCheckedIn = true;
        
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room != null) room.Status = "Occupied";

        SaveChanges();
        _logger.LogInformation($"CLIENT {clientUsername}: Check-in efectuat camera {roomNumber}");
    }

    public List<Reservation> GetReservationsForCheckOut(string clientUsername)
    {
        return Reservations.Where(r => 
            r.ClientUsername == clientUsername &&
            r.IsCheckedIn && 
            !r.IsCheckedOut
        ).ToList();
    }

    public void SelfCheckOut(string clientUsername, int roomNumber)
    {
        var res = Reservations.FirstOrDefault(r => 
            r.ClientUsername == clientUsername && 
            r.RoomNumber == roomNumber && 
            r.IsCheckedIn && 
            !r.IsCheckedOut);
        
        if (res == null) throw new Exception("Nu sunteți cazat activ în această cameră.");

        res.IsCheckedOut = true; 
        
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room != null) room.Status = "Cleaning"; 
        
        SaveChanges();
        _logger.LogInformation($"CLIENT {clientUsername}: Check-out efectuat camera {roomNumber}");
    }

    public void CancelReservation(string clientUsername, int roomNumber)
    {
        var res = Reservations.FirstOrDefault(r => r.ClientUsername == clientUsername && r.RoomNumber == roomNumber && !r.IsCheckedOut);

        if (res != null)
        {
            if (res.IsCheckedIn) throw new Exception("Nu poti anula o rezervare dupa check-in.");
            if (res.StartDate.Date < CurrentDate.Date) throw new Exception("Nu poti anula o rezervare din trecut.");

            res.IsCheckedOut = true;

            var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
            if (room != null && res.StartDate.Date == CurrentDate.Date) 
                room.Status = "Free";

            SaveChanges();
            _logger.LogInformation($"CLIENT {clientUsername}: Rezervare anulată camera {roomNumber}");
        }
        else
        {
            throw new Exception("Nu s-a gasit rezervarea activa.");
        }
    }

    public void DeleteSelfAccount(string clientUsername)
    {
        bool hasActiveReservations = Reservations.Any(r => 
            r.ClientUsername == clientUsername && !r.IsCheckedOut);

        if (hasActiveReservations)
        {
            throw new Exception("Nu va puteti sterge contul cât timp aveti rezervari active sau sunteti cazat. Finalizati sederea inainte.");
        }

        var client = Clients.FirstOrDefault(c => c.UsernameID == clientUsername);
        if (client != null)
        {
            Reservations.RemoveAll(r => r.ClientUsername == clientUsername);
            Clients.Remove(client);
            SaveChanges();
            _logger.LogInformation($"CLIENT {clientUsername}: Cont sters definitiv la cerere.");
        }
    }

    // ==========================================================
    // 3. METODE CLEANER
    // ==========================================================

    public List<Room> GetDirtyRooms()
    {
        return Rooms.Where(r => r.Status == "Cleaning").ToList();
    }

    public void CleanRoom(int roomNumber)
    {
        var room = Rooms.FirstOrDefault(r => r.RoomNumber == roomNumber);
        if (room != null && room.Status == "Cleaning")
        {
            room.Status = "Free";
            SaveChanges();
            _logger.LogInformation($"CLEANER: Camera {roomNumber} curatata.");
        }
        else
        {
            throw new Exception($"Camera {roomNumber} nu necesita cleaning sau nu exista.");
        }
    }

    // ==========================================================
    // 4. AUTH & HELPERE
    // ==========================================================

    public Persoana? Authenticate(string username, string password)
    {
        var admin = Admins.FirstOrDefault(a => a.UsernameID == username && a.Password == password);
        if (admin != null) return admin;

        var client = Clients.FirstOrDefault(c => c.UsernameID == username && c.Password == password);
        if (client != null) return client;

        var cleaner = Cleaners.FirstOrDefault(c => c.UsernameID == username && c.Password == password);
        if (cleaner != null) return cleaner;

        return null;
    }

    public void RegisterClient(string firstName, string lastName, int age, string username, string password)
    {
        // Regula: Doar 18+
        if (age < 18)
        {
            throw new Exception($"Înregistrare respinsa. Varsta minimă este 18 ani. (Varsta introdusă: {age})");
        }

        if (Clients.Any(c => c.UsernameID == username)) 
            throw new Exception("Username indisponibil.");

        Clients.Add(new Client { 
            FirstName = firstName, LastName = lastName, Age = age, 
            UsernameID = username, Password = password 
        });
        SaveChanges();
        _logger.LogInformation($"Register Client Nou: {username} (Age: {age})");
    }

    private void SaveChanges()
    {
        var data = new HotelDataWrapper
        {
            Rooms = Rooms,
            Clients = Clients,
            Admins = Admins,
            Cleaners = Cleaners,
            Reservations = Reservations,
            CheckInStart = CheckInStart,
            CheckOutLimit = CheckOutLimit
        };
        _persistence.SaveData(data);
    }

    private void EnsureSeedData()
    {
        if (!Admins.Any())
        {
            Admins.Add(new Administrator { FirstName = "Mario", LastName = "Marica", UsernameID = "admin", Password = "123" });
            Cleaners.Add(new Cleaner { FirstName = "Alex", LastName = "Barmondius", UsernameID = "cleaner", Password = "123" });
            Rooms.Add(new Room { RoomNumber = 101, PricePerNight = 100, RoomType = "Single" });
            SaveChanges();
        }
    }
}