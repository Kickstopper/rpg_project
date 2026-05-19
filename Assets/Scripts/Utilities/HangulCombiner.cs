using UnityEngine;
namespace Utilities
{
    public static class HangulCombiner
    {
        // 유니코드 한글 시작점 ("가")
        private const int HANGUL_BASE = 0xAC00;

        // 초성 (19개)
        public static readonly string[] Cho = { 
            "ㄱ", "ㄲ", "ㄴ", "ㄷ", "ㄸ", "ㄹ", "ㅁ", "ㅂ", "ㅃ", "ㅅ", 
            "ㅆ", "ㅇ", "ㅈ", "ㅉ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" 
        };

        // 중성 (21개)
        public static readonly string[] Jung = { 
            "ㅏ", "ㅐ", "ㅑ", "ㅒ", "ㅓ", "ㅔ", "ㅕ", "ㅖ", "ㅗ", "ㅘ", 
            "ㅙ", "ㅚ", "ㅛ", "ㅜ", "ㅝ", "ㅞ", "ㅟ", "ㅠ", "ㅡ", "ㅢ", "ㅣ" 
        };

        // 종성 (28개, 인덱스 0은 '종성 없음')
        public static readonly string[] Jong = { 
            "", "ㄱ", "ㄲ", "ㄳ", "ㄴ", "ㄵ", "ㄶ", "ㄷ", "ㄹ", "ㄺ", 
            "ㄻ", "ㄼ", "ㄽ", "ㄾ", "ㄿ", "ㅀ", "ㅁ", "ㅂ", "ㅄ", "ㅅ", 
            "ㅆ", "ㅇ", "ㅈ", "ㅊ", "ㅋ", "ㅌ", "ㅍ", "ㅎ" 
        };

        /// <summary>
        /// 초성, 중성, 종성 인덱스를 받아 완성된 한글 Char를 반환합니다.
        /// </summary>
        public static char Combine(int choIndex, int jungIndex, int jongIndex)
        {
            // 유니코드 공식: 0xAC00 + (초성 인덱스 * 21 * 28) + (중성 인덱스 * 28) + 종성 인덱스
            int unicode = HANGUL_BASE + (choIndex * 21 * 28) + (jungIndex * 28) + jongIndex;
            return (char)unicode;
        }
    }
    
}