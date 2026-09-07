using System;
using System.IO;
using System.Security.Cryptography;

namespace KanTab.Storage.Supabase;

public static class DeviceIdProvider
{
    private static readonly string DeviceIdFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KanTab", "device.id");

    public static string DeviceId
    {
        get
        {
            try
            {
                if (File.Exists(DeviceIdFilePath))
                {
                    var id = File.ReadAllText(DeviceIdFilePath).Trim();
                    if (!string.IsNullOrEmpty(id))
                        return id;
                }

                var newId = Guid.NewGuid().ToString();
                var directory = Path.GetDirectoryName(DeviceIdFilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(DeviceIdFilePath, newId);
                return newId;
            }
            catch
            {
                return Guid.NewGuid().ToString();
            }
        }
    }
}
