namespace PrintFleet.WorkTracker.Api.Services;

public class EmailOptions
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "WorkTracker";
    public string SiteUrl { get; set; } = "";
    public int Hour { get; set; } = 17;
    public int Minute { get; set; } = 0;
    public string[] Recipients { get; set; } = [];

    public bool CanSend =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && !string.IsNullOrWhiteSpace(FromAddress);

    public bool IsConfigured => CanSend && Recipients.Length > 0;
}
