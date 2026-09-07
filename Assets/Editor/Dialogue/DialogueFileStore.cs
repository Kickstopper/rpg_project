using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DialogueEditing
{
    public static class DialogueFileStore
    {
        public const string CsvPath = "Assets/CSV/Dialogues/EventScripts.csv";
        public const string DraftPath = "UserSettings/DialogueEditor/EventScripts.draft.json";

        public static string Hash(string path)
        {
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        public static string ReadSnapshot(string path, out string hash)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (var sha = SHA256.Create()) hash = Convert.ToBase64String(sha.ComputeHash(bytes));
            return Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        }

        public static string Save(string path, string csv, string expectedHash, string backupDirectory)
        {
            if (!File.Exists(path) || Hash(path) != expectedHash)
                throw new IOException("CSV가 외부에서 변경되거나 삭제되었습니다. 현재 초안은 유지됩니다. 초안을 내보낸 후 다시 불러와 변경 내용을 합쳐 주세요.");
            Directory.CreateDirectory(backupDirectory);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backup = Path.Combine(backupDirectory, "EventScripts." + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "." + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                File.WriteAllText(temp, csv, new UTF8Encoding(true));
                if (Hash(path) != expectedHash) throw new IOException("저장 도중 CSV가 변경되었습니다. 덮어쓰지 않았습니다.");
                File.Replace(temp, path, backup);
                return backup;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
