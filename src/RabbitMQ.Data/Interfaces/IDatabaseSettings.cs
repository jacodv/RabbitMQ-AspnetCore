namespace UtilityData.Data.Interfaces
{
  public interface IDatabaseSettings
  {
    string DatabaseName { get; set; }
    string ConnectionString { get; set; }
    string Secret { get; set; }
    bool DisableTrace { get; set; }
    string ApplicationName { get; set; }
  }
}