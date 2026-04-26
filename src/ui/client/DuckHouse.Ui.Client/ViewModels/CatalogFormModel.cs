using System.ComponentModel.DataAnnotations;

namespace DuckHouse.Ui.Client.ViewModels;

public class CatalogFormModel
{
    public string Name { get; set; } = "";
    public bool IsManaged { get; set; } = true;
    public bool AllowExistingDatabase { get; set; }
    public string DataPath { get; set; } = "";
    public string StorageConnectionString { get; set; } = "";
    public string CatalogHost { get; set; } = "";
    public int CatalogPort { get; set; } = 5432;
    public string CatalogDatabase { get; set; } = "";
    public string CatalogUser { get; set; } = "";
    public string CatalogPassword { get; set; } = "";
}
