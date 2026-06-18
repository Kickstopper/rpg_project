using System;
using System.Collections.Generic;

namespace Helper
{
    public class HangulCombiner
    {
        private const ushort HANGUL_BASE = 0xAC00;

        private static readonly char[] CHO = { 'ㄱ','ㄲ','ㄴ','ㄷ','ㄸ','ㄹ','ㅁ','ㅂ','ㅃ','ㅅ','ㅆ','ㅇ','ㅈ','ㅉ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ' };
        private static readonly char[] JUNG = { 'ㅏ','ㅐ','ㅑ','ㅒ','ㅓ','ㅔ','ㅕ','ㅖ','ㅗ','ㅘ','ㅙ','ㅚ','ㅛ','ㅜ','ㅝ','ㅞ','ㅟ','ㅠ','ㅡ','ㅢ','ㅣ' };
        private static readonly char[] JONG = { '\0','ㄱ','ㄲ','ㄳ','ㄴ','ㄵ','ㄶ','ㄷ','ㄹ','ㄺ','ㄻ','ㄼ','ㄽ','ㄾ','ㄿ','ㅀ','ㅁ','ㅂ','ㅄ','ㅅ','ㅆ','ㅇ','ㅈ','ㅊ','ㅋ','ㅌ','ㅍ','ㅎ' };

        // 이중 모음 매핑 (앞 모음 + 뒤 모음 = 완성된 이중 모음)
        private static readonly Dictionary<(char, char), char> DoubleJungMap = new Dictionary<(char, char), char>
        {
            {('ㅗ', 'ㅏ'), 'ㅘ'}, {('ㅗ', 'ㅐ'), 'ㅙ'}, {('ㅗ', 'ㅣ'), 'ㅚ'},
            {('ㅜ', 'ㅓ'), 'ㅝ'}, {('ㅜ', 'ㅔ'), 'ㅞ'}, {('ㅜ', 'ㅣ'), 'ㅟ'},
            {('ㅡ', 'ㅣ'), 'ㅢ'}
        };

        // 겹받침 매핑 (앞 자음 + 뒤 자음 = 완성된 겹받침)
        private static readonly Dictionary<(char, char), char> DoubleJongMap = new Dictionary<(char, char), char>
        {
            {('ㄱ', 'ㅅ'), 'ㄳ'}, {('ㄴ', 'ㅈ'), 'ㄵ'}, {('ㄴ', 'ㅎ'), 'ㄶ'},
            {('ㄹ', 'ㄱ'), 'ㄺ'}, {('ㄹ', 'ㅁ'), 'ㄻ'}, {('ㄹ', 'ㅂ'), 'ㄼ'},
            {('ㄹ', 'ㅅ'), 'ㄽ'}, {('ㄹ', 'ㅌ'), 'ㄾ'}, {('ㄹ', 'ㅍ'), 'ㄿ'},
            {('ㄹ', 'ㅎ'), 'ㅀ'}, {('ㅂ', 'ㅅ'), 'ㅄ'}
        };

        // 연음 처리를 위한 겹받침 분리 매핑 (예: ㄺ + 모음 -> ㄹ(남음), ㄱ(넘어감))
        private static readonly Dictionary<char, (char keepJong, char carryCho)> SplitJongForCarryMap = new Dictionary<char, (char, char)>
        {
            {'ㄳ', ('ㄱ', 'ㅅ')}, {'ㄵ', ('ㄴ', 'ㅈ')}, {'ㄶ', ('ㄴ', 'ㅎ')},
            {'ㄺ', ('ㄹ', 'ㄱ')}, {'ㄻ', ('ㄹ', 'ㅁ')}, {'ㄼ', ('ㄹ', 'ㅂ')},
            {'ㄽ', ('ㄹ', 'ㅅ')}, {'ㄾ', ('ㄹ', 'ㅌ')}, {'ㄿ', ('ㄹ', 'ㅍ')},
            {'ㅀ', ('ㄹ', 'ㅎ')}, {'ㅄ', ('ㅂ', 'ㅅ')}
        };

        // 백스페이스용 분리 매핑
        private static readonly Dictionary<char, char> ReverseDoubleJungMap = new Dictionary<char, char>
        {
            {'ㅘ', 'ㅗ'}, {'ㅙ', 'ㅗ'}, {'ㅚ', 'ㅗ'}, {'ㅝ', 'ㅜ'}, {'ㅞ', 'ㅜ'}, {'ㅟ', 'ㅜ'}, {'ㅢ', 'ㅡ'}
        };
        private static readonly Dictionary<char, char> ReverseDoubleJongMap = new Dictionary<char, char>
        {
            {'ㄳ', 'ㄱ'}, {'ㄵ', 'ㄴ'}, {'ㄶ', 'ㄴ'}, {'ㄺ', 'ㄹ'}, {'ㄻ', 'ㄹ'}, {'ㄼ', 'ㄹ'},
            {'ㄽ', 'ㄹ'}, {'ㄾ', 'ㄹ'}, {'ㄿ', 'ㄹ'}, {'ㅀ', 'ㄹ'}, {'ㅄ', 'ㅂ'}
        };

        private int currentCho = -1;
        private int currentJung = -1;
        private int currentJong = -1;

        public string InputChar(string currentText, char inputChar)
        {
            int choIndex = Array.IndexOf(CHO, inputChar);
            int jungIndex = Array.IndexOf(JUNG, inputChar);

            // 자음이 입력된 경우
            if (choIndex >= 0)
            {
                if (currentCho == -1) 
                {
                    currentCho = choIndex;
                    return currentText + inputChar;
                }
                else if (currentJung != -1 && currentJong == -1) 
                {
                    int jongIndex = Array.IndexOf(JONG, inputChar);
                    if (jongIndex > 0)
                    {
                        currentJong = jongIndex;
                        return ReplaceLastChar(currentText, Combine());
                    }
                }
                else if (currentJung != -1 && currentJong > 0)
                {
                    // 겹받침 조합 시도 (예: ㄱ + ㅅ -> ㄳ)
                    char currentJongChar = JONG[currentJong];
                    if (DoubleJongMap.TryGetValue((currentJongChar, inputChar), out char doubleJong))
                    {
                        currentJong = Array.IndexOf(JONG, doubleJong);
                        return ReplaceLastChar(currentText, Combine());
                    }
                }
                
                ResetState();
                currentCho = choIndex;
                return currentText + inputChar;
            }
            // 모음이 입력된 경우
            else if (jungIndex >= 0)
            {
                if (currentCho != -1 && currentJung == -1) 
                {
                    currentJung = jungIndex;
                    return ReplaceLastChar(currentText, Combine());
                }
                else if (currentCho != -1 && currentJung != -1 && currentJong == -1)
                {
                    // 이중 모음 조합 시도 (예: ㅗ + ㅏ -> ㅘ)
                    char currentJungChar = JUNG[currentJung];
                    if (DoubleJungMap.TryGetValue((currentJungChar, inputChar), out char doubleJung))
                    {
                        currentJung = Array.IndexOf(JUNG, doubleJung);
                        return ReplaceLastChar(currentText, Combine());
                    }
                }
                else if (currentCho != -1 && currentJung != -1 && currentJong != -1)
                {
                    char currentJongChar = JONG[currentJong];

                    // 겹받침 연음 처리 (예: 닭 + ㅏ -> 달가)
                    if (SplitJongForCarryMap.TryGetValue(currentJongChar, out var splitResult))
                    {
                        currentJong = Array.IndexOf(JONG, splitResult.keepJong);
                        currentText = ReplaceLastChar(currentText, Combine());

                        ResetState();
                        currentCho = Array.IndexOf(CHO, splitResult.carryCho);
                        currentJung = jungIndex;
                        return currentText + Combine();
                    }
                    // 단일 받침 연음 처리 (예: 각 + ㅏ -> 가가)
                    else
                    {
                        int prevJongAsCho = Array.IndexOf(CHO, currentJongChar);
                        currentJong = -1;
                        currentText = ReplaceLastChar(currentText, Combine());

                        ResetState();
                        currentCho = prevJongAsCho;
                        currentJung = jungIndex;
                        return currentText + Combine();
                    }
                }

                ResetState();
                return currentText + inputChar;
            }

            ResetState();
            return currentText + inputChar;
        }

        public string DeleteChar(string currentText)
        {
            if (string.IsNullOrEmpty(currentText)) return "";

            char lastChar = currentText[currentText.Length - 1];

            if (lastChar >= 0xAC00 && lastChar <= 0xD7A3)
            {
                int code = lastChar - HANGUL_BASE;
                currentJong = code % 28;
                currentJung = ((code - currentJong) / 28) % 21;
                currentCho = (((code - currentJong) / 28) - currentJung) / 21;

                if (currentJong > 0) 
                {
                    // 겹받침 지우기 (예: 핥 -> 할)
                    if (ReverseDoubleJongMap.TryGetValue(JONG[currentJong], out char firstJong))
                    {
                        currentJong = Array.IndexOf(JONG, firstJong);
                        return ReplaceLastChar(currentText, Combine());
                    }
                    currentJong = -1;
                    return ReplaceLastChar(currentText, Combine());
                }
                else if (currentJung > 0) 
                {
                    // 이중 모음 지우기 (예: 와 -> 오)
                    if (ReverseDoubleJungMap.TryGetValue(JUNG[currentJung], out char firstJung))
                    {
                        currentJung = Array.IndexOf(JUNG, firstJung);
                        return ReplaceLastChar(currentText, Combine());
                    }
                    currentJung = -1;
                    return ReplaceLastChar(currentText, CHO[currentCho]);
                }
            }

            ResetState();
            return currentText.Substring(0, currentText.Length - 1);
        }

        private char Combine()
        {
            int c = Math.Max(0, currentCho);
            int v = Math.Max(0, currentJung);
            int t = Math.Max(0, currentJong);
            
            return (char)(HANGUL_BASE + (c * 21 * 28) + (v * 28) + t);
        }

        private string ReplaceLastChar(string text, char newChar)
        {
            if (text.Length == 0) return newChar.ToString();
            return text.Substring(0, text.Length - 1) + newChar;
        }

        // 상태 백업용 메서드
        public void CloneState(out int cho, out int jung, out int jong)
        {
            cho = currentCho;
            jung = currentJung;
            jong = currentJong;
        }
        // 복구용 메서드
        public void RestoreState(int cho, int jung, int jong)
        {
            currentCho = cho;
            currentJung = jung;
            currentJong = jong;
        }

        public void ResetState()
        {
            currentCho = -1;
            currentJung = -1;
            currentJong = -1;
        }
    }
}