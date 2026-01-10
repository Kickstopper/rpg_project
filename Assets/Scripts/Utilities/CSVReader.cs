using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

public static class CSVReader
{
    // CSV 문자열을 파싱하여 List<Dictionary<string, string>> 형태로 반환
    // 따옴표 안의 쉼표(,)는 분리하지 않는 정규식 사용
    static string SPLIT_RE = @",(?=(?:[^""]*""[^""]*"")*(?![^""]*""))";
    static string LINE_SPLIT_RE = @"\r\n|\n\r|\n|\r";

    public static List<Dictionary<string, string>> Read(TextAsset data)
    {
        var list = new List<Dictionary<string, string>>();
        var lines = Regex.Split(data.text, LINE_SPLIT_RE);

        if (lines.Length <= 1) return list;

        // 첫 줄(Header) 파싱
        var header = Regex.Split(lines[0], SPLIT_RE);
        
        // 데이터 파싱
        for (var i = 1; i < lines.Length; i++)
        {
            var values = Regex.Split(lines[i], SPLIT_RE);
            if (values.Length == 0 || values[0] == "") continue;

            var entry = new Dictionary<string, string>();
            for (var j = 0; j < header.Length && j < values.Length; j++)
            {
                string value = values[j];
                // 앞뒤 따옴표 제거 및 이스케이프 문자 처리
                value = value.TrimStart('\"').TrimEnd('\"').Replace("\"\"", "\"");
                entry[header[j]] = value;
            }
            list.Add(entry);
        }
        return list;
    }
}