namespace SchoolAdmission.Api.Models; // models namespace

public enum HoldStatus { Held = 0, Confirmed = 1, Cancelled = 2, Expired = 3 } // hold-er obostha (DB-te int)

public class ClassRoom // ekta class (shift shoho)
{
    public string Id { get; set; } = ""; // primary key, jemon c_abc123
    public string Name { get; set; } = ""; // class-er nam
    public int TotalSeats { get; set; } // mot seat
    public DateTime CreatedAt { get; set; } // toiri somoy UTC
}

public class Hold // ekta abedon / seat hold
{
    public string Id { get; set; } = ""; // primary key, jemon h_abc123
    public string ClassId { get; set; } = ""; // kon class
    public ClassRoom? Class { get; set; } // navigation
    public string StudentId { get; set; } = ""; // 17 ongker student id
    public DateTime CreatedAt { get; set; } // hold shuru
    public DateTime ExpiresAt { get; set; } // meyad shesh
    public HoldStatus Status { get; set; } // obostha
    public DateTime? ConfirmedAt { get; set; } // confirm somoy
}