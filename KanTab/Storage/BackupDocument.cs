using System;
using System.Collections.Generic;
using KanTab.Models;

namespace KanTab.Storage;

/// <summary>
/// External backup snapshot. Never contains secrets/credentials.
/// ViewModels are not serialized directly — only the explicit DTO.
/// </summary>
public class BackupDocument
{
    public int FormatVersion { get; set; }
    public DateTime ExportedAt { get; set; }
    public BackupPayload Data { get; set; } = new();
}

public class BackupPayload
{
    public List<Board> Boards { get; set; } = new();
    public List<Note> Notes { get; set; } = new();
}
