using System.Threading.Tasks;

namespace KanTab.Services;

public interface IFileDialogService
{
    Task<string?> ShowSaveDialogAsync(string suggestedFileName, string title = "Export Backup");
    Task<string?> ShowOpenDialogAsync(string title = "Import Backup");
    Task<string> ReadTextAsync(string path);
    Task WriteTextAsync(string path, string content);
    Task<bool> ShowConfirmAsync(string title, string message);
    void ShowMessage(string title, string message);
}
