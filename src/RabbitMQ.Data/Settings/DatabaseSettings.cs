using UtilityData.Data.Interfaces;

namespace UtilityData.Data.Settings
{
  public class DatabaseSettings : IDatabaseSettings
  {
    public string DatabaseName { get; set; } = "UtilityData";
    public string ConnectionString { get; set; } = "DatabaseName";
    public string Secret { get; set; } = "S0meDAt@BaseS3cret!";
    public bool DisableTrace { get; set; }
    public string ApplicationName { get; set; } = "NotSet";
  }
}
