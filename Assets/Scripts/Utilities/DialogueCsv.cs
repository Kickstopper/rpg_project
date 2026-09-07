using System;
using System.Collections.Generic;
using System.Text;

/// <summary>CSV codec shared by the dialogue editor and dialogue runtime only.</summary>
public static class DialogueCsv
{
    public static List<string[]> Parse(string text)
    {
        var records = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, closedQuote = false, started = false;
        text = text ?? "";
        for (int i = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(c);
                continue;
            }
            if (c == ',' || c == '\r' || c == '\n')
            {
                fields.Add(field.ToString()); field.Clear(); closedQuote = false; started = false;
                if (c == ',') continue;
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                records.Add(fields.ToArray()); fields.Clear();
                continue;
            }
            if (closedQuote) throw new FormatException($"CSV {records.Count + 1}번째 레코드: 닫는 따옴표 뒤에 문자가 있습니다.");
            if (c == '"')
            {
                if (started) throw new FormatException($"CSV {records.Count + 1}번째 레코드: 따옴표 위치가 잘못되었습니다.");
                quoted = true; started = true;
            }
            else { field.Append(c); started = true; }
        }
        if (quoted) throw new FormatException("CSV의 마지막 따옴표가 닫히지 않았습니다.");
        if (started || closedQuote || fields.Count > 0)
        {
            fields.Add(field.ToString()); records.Add(fields.ToArray());
        }
        return records;
    }

    public static string Write(IEnumerable<string[]> records)
    {
        var text = new StringBuilder();
        foreach (string[] record in records)
        {
            for (int i = 0; i < record.Length; i++)
            {
                if (i > 0) text.Append(',');
                string value = record[i] ?? "";
                if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0)
                    text.Append('"').Append(value.Replace("\"", "\"\"")).Append('"');
                else text.Append(value);
            }
            text.Append('\n');
        }
        return text.ToString();
    }

    public static List<Dictionary<string, string>> Read(string text)
    {
        List<string[]> records = Parse(text);
        var result = new List<Dictionary<string, string>>();
        if (records.Count == 0) return result;
        string[] header = records[0];
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (string key in header)
            if (string.IsNullOrWhiteSpace(key) || !keys.Add(key)) throw new FormatException("CSV 헤더가 비어 있거나 중복됩니다.");
        for (int i = 1; i < records.Count; i++)
        {
            string[] values = records[i];
            bool empty = true;
            foreach (string value in values) if (!string.IsNullOrEmpty(value)) { empty = false; break; }
            if (empty) continue;
            if (values.Length != header.Length) throw new FormatException($"CSV {i + 1}번째 레코드: 열 수 {values.Length}, 필요한 열 수 {header.Length}.");
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int j = 0; j < header.Length; j++) row.Add(header[j], values[j]);
            result.Add(row);
        }
        return result;
    }
}
