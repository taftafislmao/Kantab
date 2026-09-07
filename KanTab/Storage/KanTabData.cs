using System.Collections.Generic;
using KanTab.Models;

namespace KanTab.Storage;

/// <summary>
/// The single root document persisted by KanTab. Everything the user creates
/// lives here so boards, tasks, and notes stay consistent on disk.
/// </summary>
public class KanTabData
{
    public List<Board> Boards { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
    public AppSettings Settings { get; set; } = new();
}
