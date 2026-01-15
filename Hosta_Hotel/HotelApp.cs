using Microsoft.Extensions.Hosting;
using Hosta_Hotel.TheBrain;
using Hosta_Hotel.Entities;
using System.Globalization;

namespace Hosta_Hotel;

public class HotelApp : IHostedService
{
    private readonly Hotel _hotel;
    private readonly IHostApplicationLifetime _appLifetime;

    public HotelApp(Hotel hotel, IHostApplicationLifetime appLifetime)
    {
        _hotel = hotel;
        _appLifetime = appLifetime;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Task.Run(() => MainLoop());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void MainLoop()
    {
        // La pornire, setam data simulata
        SetSimulationDate();

        bool running = true;
        while (running)
        {
            ShowHeader("MAIN MENU");
            Console.WriteLine("1. Login");
            Console.WriteLine("2. Register");
            Console.WriteLine("3. Change Hotel Date (Simulare)");
            Console.WriteLine("4. Exit");
            Console.Write("Selectati o optiune: ");

            try
            {
                switch (Console.ReadLine())
                {
                    case "1": HandleLogin(); break;
                    case "2": HandleRegister(); break;
                    case "3": SetSimulationDate(); break;
                    case "4": running = false; break;
                    default: Console.WriteLine("Optiune invalida."); break;
                }
            }
            catch (Exception ex) { PrintErrorMessage(ex.Message); }
        }
        _appLifetime.StopApplication();
    }

    // --- UTILS ---

    private void SetSimulationDate()
    {
        Console.Clear();
        Console.WriteLine("=== CONFIGURARE DATA HOTEL ===");
        Console.WriteLine($"Data curenta a hotelului: {_hotel.CurrentDate:dd-MM-yyyy}");
        Console.WriteLine("Apasati ENTER pentru a pastra data de azi SAU introduceti o noua data (dd-mm-yyyy):");

        string input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            _hotel.SetSimulationDate(DateTime.Now);
        }
        else
        {
            if (DateTime.TryParseExact(
                    input,
                    "dd-MM-yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime newDate))
            {
                if (newDate.Year < 2020 || newDate.Year > 2050)
                {
                    PrintErrorMessage("Anul introdus este invalid (Trebuie sa fie intre 2020 si 2050).");
                    return;
                }

                _hotel.SetSimulationDate(newDate);
                Console.WriteLine("Data actualizata!");
            }
            else
            {
                Console.WriteLine("Format invalid. Se foloseste data de azi.");
                _hotel.SetSimulationDate(DateTime.Now);
            }
        }
        Thread.Sleep(500);
    }

    private void ShowHeader(string menuName)
    {
        Console.Clear();
        Console.WriteLine("========================================");
        Console.WriteLine($"      HOSTA HOTEL - {menuName}");
        Console.WriteLine("========================================");
        Console.WriteLine($"[DATE]: {_hotel.CurrentDate:dd-MM-yyyy} (Simulat)");
        Console.WriteLine($"[TIME]: {DateTime.Now:HH:mm} (Real)");
        Console.WriteLine("----------------------------------------");
    }

    // --- AUTH ---

    private void HandleRegister()
    {
        ShowHeader("INREGISTRARE");
        try 
        {
            Console.Write("Prenume: "); string first = Console.ReadLine();
            Console.Write("Nume: "); string last = Console.ReadLine();
            
            // [MODIFICARE] Cerinta explicita 18+
            Console.Write("Varsta (18+): "); 
            int age = int.Parse(Console.ReadLine());

            Console.Write("Username: "); string user = Console.ReadLine();
            Console.Write("Parola: "); string pass = Console.ReadLine();

            // Metoda din Hotel va arunca exceptie daca age < 18
            _hotel.RegisterClient(first, last, age, user, pass);
            
            Console.WriteLine("Succes! Cont creat. Apasati o tasta.");
        }
        catch (FormatException)
        {
            PrintErrorMessage("Formatul introdus (de ex. la varsta) nu este valid.");
            return;
        }
        catch (Exception ex)
        {
            PrintErrorMessage(ex.Message);
            return;
        }
        Console.ReadKey();
    }

    private void HandleLogin()
    {
        ShowHeader("LOGIN");
        Console.Write("Username: "); string user = Console.ReadLine();
        Console.Write("Parola: "); string pass = Console.ReadLine();

        var person = _hotel.Authenticate(user, pass);

        if (person == null)
        {
            Console.WriteLine("User sau parola gresita.");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"Bun venit, {person.FirstName}!");
        Thread.Sleep(800);

        if (person is Administrator admin) MenuAdmin(admin);
        else if (person is Client client) MenuClient(client);
        else if (person is Cleaner cleaner) MenuCleaner(cleaner);
    }

    // --- ADMIN MENU ---

    private void MenuAdmin(Administrator admin)
    {
        bool back = false;
        while (!back)
        {
            ShowHeader($"ADMIN PANEL: {admin.LastName}");
            Console.WriteLine("1. Gestiune Camere (Add/Remove/Status)");
            Console.WriteLine("2. Gestiune Cleaneri (Add/Remove)");
            Console.WriteLine("3. Configurare Reguli (CheckIn/Out Time)");
            Console.WriteLine("4. Vezi Rezervari Client");
            Console.WriteLine("5. Modifica o Rezervare");
            Console.WriteLine("6. Sterge Cont Client");
            Console.WriteLine("7. Logout");
            Console.Write("Optiune: ");

            try
            {
                switch (Console.ReadLine())
                {
                    case "1": SubMenuRooms(); break;
                    case "2": SubMenuCleaners(); break;
                    case "3":
                        Console.WriteLine("1. Set CheckIn Start | 2. Set CheckOut Limit");
                        string ruleOpt = Console.ReadLine();
                        Console.Write("Ora noua (HH:mm): ");
                        TimeSpan ts = TimeSpan.Parse(Console.ReadLine());
                        if (ruleOpt == "1") _hotel.UpdateCheckInTime(ts);
                        else _hotel.UpdateCheckOutTime(ts);
                        Console.WriteLine("Regula actualizata.");
                        Console.ReadKey();
                        break;
                    case "4":
                        Console.Write("Username Client: ");
                        string cUser = Console.ReadLine();
                        var resList = _hotel.GetClientReservations(cUser);
                        if (resList.Count == 0) Console.WriteLine("Nicio rezervare gasita.");
                        foreach (var r in resList)
                            Console.WriteLine($"Camera {r.RoomNumber} | {r.StartDate:dd-MM-yyyy}-{r.EndDate:dd-MM-yyyy} | CheckIn: {r.IsCheckedIn} | Out: {r.IsCheckedOut}");
                        Console.ReadKey();
                        break;
                    case "5": ManageReservationFlow(); break;
                    
                    // [MODIFICARE] Optiunea de stergere client
                    case "6":
                        Console.Write("Introdu Username-ul clientului de sters: ");
                        string targetUser = Console.ReadLine();
                        Console.WriteLine($"ATENTIE: Se vor sterge si rezervarile istorice pentru '{targetUser}'. Continuati? (da/nu)");
                        if(Console.ReadLine().ToLower() == "da")
                        {
                            _hotel.AdminDeleteClient(targetUser);
                            Console.WriteLine("Client sters cu succes.");
                        }
                        else Console.WriteLine("Anulat.");
                        Console.ReadKey();
                        break;

                    case "7": back = true; break;
                }
            }
            catch (Exception ex) { PrintErrorMessage(ex.Message); }
        }
    }

    private void SubMenuRooms()
    {
        ShowHeader("CAMERE");
        Console.WriteLine("1. Adauga Camera");
        Console.WriteLine("2. Sterge Camera");
        Console.WriteLine("3. Schimba Status");
        Console.WriteLine("4. Vezi Lista");
        string opt = Console.ReadLine();

        if (opt == "1")
        {
            Console.Write("Nr Camera: "); int nr = int.Parse(Console.ReadLine());
            Console.WriteLine("Tip Camera: 1. Single | 2. Double | 3. Suite");
            string typeOpt = Console.ReadLine();
            string type = "Single";
            if (typeOpt == "2") type = "Double";
            if (typeOpt == "3") type = "Suite";

            Console.Write("Pret (RON): "); int price = int.Parse(Console.ReadLine());
            _hotel.AddRoom(nr, type, price);
            Console.WriteLine($"Camera {nr} ({type}) adaugata.");
            Console.ReadKey();
        }
        else if (opt == "2")
        {
            Console.Write("Nr Camera: "); int nr = int.Parse(Console.ReadLine());
            _hotel.RemoveRoom(nr);
            Console.WriteLine("Stearsa.");
            Console.ReadKey();
        }
        else if (opt == "3")
        {
            Console.Write("Nr Camera: "); int nr = int.Parse(Console.ReadLine());
            Console.WriteLine("Statusuri: Free, Occupied, Cleaning, Indisponible");
            Console.Write("Nou Status: "); string st = Console.ReadLine();
            _hotel.SetRoomStatus(nr, st);
        }
        else if (opt == "4")
        {
            foreach (var r in _hotel.Rooms)
                Console.WriteLine($"{r.RoomNumber} - {r.RoomType} - {r.Status} - {r.PricePerNight} RON");
            Console.ReadKey();
        }
    }

    private void SubMenuCleaners()
    {
        ShowHeader("CLEANERI");
        Console.WriteLine("1. Angajeaza | 2. Concediaza | 3. Lista");
        string opt = Console.ReadLine();

        if (opt == "1")
        {
            Console.Write("User: "); string u = Console.ReadLine();
            Console.Write("Pass: "); string p = Console.ReadLine();
            Console.Write("Nume: "); string n = Console.ReadLine();
            Console.Write("Prenume: "); string pn = Console.ReadLine();
            _hotel.AddCleaner(n, pn, u, p);
            Console.ReadKey();
        }
        else if (opt == "2")
        {
            Console.Write("Username cleaner: "); string u = Console.ReadLine();
            _hotel.RemoveCleaner(u);
            Console.ReadKey();
        }
        else if (opt == "3")
        {
            foreach (var c in _hotel.Cleaners)
                Console.WriteLine($"{c.FirstName} {c.LastName} ({c.UsernameID})");
            Console.ReadKey();
        }
    }

    private void ManageReservationFlow()
    {
        Console.Write("Client Username: "); string user = Console.ReadLine();
        Console.Write("Room Number: "); int nr = int.Parse(Console.ReadLine());

        Console.WriteLine("Actiune: 1. Anuleaza | 2. Schimba Perioada | 3. Forteaza CheckIn");
        string act = Console.ReadLine();

        if (act == "1")
        {
            _hotel.AdminCancelReservation(user, nr);
            Console.WriteLine("Anulata.");
        }
        else if (act == "2")
        {
            Console.Write("Data Start (dd-mm-yyyy): ");
            DateTime start = DateTime.ParseExact(Console.ReadLine(), "dd-MM-yyyy", CultureInfo.InvariantCulture);

            Console.Write("Data End (dd-mm-yyyy): ");
            DateTime end = DateTime.ParseExact(Console.ReadLine(), "dd-MM-yyyy", CultureInfo.InvariantCulture);

            _hotel.AdminChangeReservationPeriod(user, nr, start, end);
            Console.WriteLine("Perioada modificata.");
        }
        else if (act == "3")
        {
            _hotel.AdminForceCheckIn(user, nr);
            Console.WriteLine("Check-in fortat executat.");
        }
        Console.ReadKey();
    }

    // --- CLIENT MENU ---

    private void MenuClient(Client client)
    {
        bool back = false;
        while (!back)
        {
            ShowHeader($"CLIENT: {client.FirstName}");
            Console.WriteLine("1. Cauta Camere");
            Console.WriteLine("2. Rezerva Camera");
            Console.WriteLine("3. Self Check-In");
            Console.WriteLine("4. Self Check-Out");
            Console.WriteLine("5. Istoric / Rezervarile mele");
            Console.WriteLine("6. Anuleaza o Rezervare");
            Console.WriteLine("7. Logout");
            Console.WriteLine("8. Sterge contul meu");
            Console.Write("Optiune: ");

            try
            {
                switch (Console.ReadLine())
                {
                    case "1":
                        var free = _hotel.Rooms.Where(r => r.Status == "Free").ToList();
                        Console.WriteLine("Disponibile:");
                        foreach (var r in free)
                            Console.WriteLine($"{r.RoomNumber} ({r.RoomType}) - {r.PricePerNight} RON");
                        Console.ReadKey();
                        break;

                    case "2":
                        Console.Write("Nr Camera: "); int nr = int.Parse(Console.ReadLine());
                        Console.Write("Data Start (dd-mm-yyyy): ");
                        DateTime start = DateTime.ParseExact(Console.ReadLine(), "dd-MM-yyyy", CultureInfo.InvariantCulture);
                        Console.Write("Numar Zile: "); int days = int.Parse(Console.ReadLine());

                        _hotel.MakeReservation(client.UsernameID, nr, start, days);
                        Console.WriteLine("Rezervare reusita!");
                        Console.ReadKey();
                        break;

                    case "3":
                        Console.WriteLine("--- REZERVARI PENTRU CHECK-IN ---");
                        var listCheckIn = _hotel.GetReservationsForCheckIn(client.UsernameID);
                        if (listCheckIn.Count == 0)
                        {
                            Console.WriteLine("Nu aveti rezervari valide pentru check-in azi.");
                        }
                        else
                        {
                            foreach (var r in listCheckIn)
                                Console.WriteLine($"Camera {r.RoomNumber} | Start: {r.StartDate:dd-MM-yyyy}");

                            Console.Write("Introdu Nr Camera pentru Check-In: ");
                            int nrCI = int.Parse(Console.ReadLine());
                            _hotel.SelfCheckIn(client.UsernameID, nrCI);
                            Console.WriteLine("Bine ati venit!");
                        }
                        Console.ReadKey();
                        break;

                    case "4":
                        Console.WriteLine("--- CAMERE OCUPATE (CHECK-OUT) ---");
                        var listCheckOut = _hotel.GetReservationsForCheckOut(client.UsernameID);
                        if (listCheckOut.Count == 0)
                        {
                            Console.WriteLine("Nu sunteti cazat in nicio camera.");
                        }
                        else
                        {
                            foreach (var r in listCheckOut)
                                Console.WriteLine($"Camera {r.RoomNumber} | End: {r.EndDate:dd-MM-yyyy}");

                            Console.Write("Introdu Nr Camera pentru Check-Out: ");
                            int nrCO = int.Parse(Console.ReadLine());
                            _hotel.SelfCheckOut(client.UsernameID, nrCO);
                            Console.WriteLine("La revedere!");
                        }
                        Console.ReadKey();
                        break;

                    case "5":
                        var history = _hotel.GetClientReservations(client.UsernameID);
                        foreach (var h in history)
                            Console.WriteLine($"Camera {h.RoomNumber} | {h.StartDate:dd-MM-yyyy}-{h.EndDate:dd-MM-yyyy} | In: {h.IsCheckedIn} | Out: {h.IsCheckedOut}");
                        Console.ReadKey();
                        break;

                    case "6":
                        Console.Write("Nr Camera de anulat: "); int nrCan = int.Parse(Console.ReadLine());
                        _hotel.CancelReservation(client.UsernameID, nrCan);
                        Console.WriteLine("Rezervarea a fost anulata.");
                        Console.ReadKey();
                        break;

                    case "7":
                        back = true;
                        break;

                    // [MODIFICARE] Logica de stergere cont propriu
                    case "8":
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("AVERTISMENT: Aceasta actiune este ireversibila! - Please reconsider ;-;");
                        Console.ResetColor();
                        Console.WriteLine("Scrie 'STERGE' pentru a confirma stergerea contului tau:");
                        
                        string confirm = Console.ReadLine();
                        if (confirm == "STERGE")
                        {
                            _hotel.DeleteSelfAccount(client.UsernameID);
                            Console.WriteLine("Contul a fost sters. La revedere.");
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine("Ai fost cel mai bun , see you on the other side. o7");
                            Console.ResetColor();
                            Console.ReadKey();
                            back = true; // Fortam iesirea din meniul de client
                        }
                        else
                        {
                            Console.WriteLine("Nu s-a confirmat. Contul ramane activ.");
                            Console.ReadKey();
                        }
                        break;
                }
            }
            catch (Exception ex) { PrintErrorMessage(ex.Message); }
        }
    }

    // --- CLEANER MENU ---

    private void MenuCleaner(Cleaner cleaner)
    {
        bool back = false;
        while (!back)
        {
            ShowHeader($"CLEANER: {cleaner.FirstName}");
            Console.WriteLine("1. Vezi camere murdare");
            Console.WriteLine("2. Curata camera");
            Console.WriteLine("3. Logout");
            Console.Write("Optiune: ");

            try
            {
                switch (Console.ReadLine())
                {
                    case "1":
                        var dirty = _hotel.GetDirtyRooms();
                        if (!dirty.Any()) Console.WriteLine("Totul e curat.");
                        foreach (var r in dirty) Console.WriteLine($"Camera {r.RoomNumber}");
                        Console.ReadKey();
                        break;
                    case "2":
                        Console.Write("Nr Camera: "); int nr = int.Parse(Console.ReadLine());
                        _hotel.CleanRoom(nr);
                        Console.WriteLine("Camera marcata Free.");
                        Console.ReadKey();
                        break;
                    case "3":
                        back = true;
                        break;
                }
            }
            catch (Exception ex) { PrintErrorMessage(ex.Message); }
        }
    }

    private void PrintErrorMessage(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"EROARE: {message}");
        Console.ResetColor();
        Console.WriteLine("Apasati orice tasta...");
        Console.ReadKey();
    }
}