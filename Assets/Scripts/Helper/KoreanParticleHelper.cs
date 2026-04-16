using System;

namespace Helper
{
    public static class KoreanParticleHelper
    {
        // 한글 유니코드의 시작과 끝
        private const int HANGUL_BASE = 0xAC00;     // '가'
        private const int HANGUL_END = 0xD7A3;      // '힣'

        public static string AttachParticle(this string word, string particleFormat)
        {
            if (string.IsNullOrEmpty(word)) return word;

            char lastChar = word[word.Length - 1];

            // 마지막 글자가 한글인지 확인
            if (lastChar < HANGUL_BASE || lastChar > HANGUL_END)
            {
                // 한글이 아니라면 (영어, 숫자 등) 기본적으로 앞의 조사를 붙이거나 원래 문자열 반환
                return word + particleFormat.Split('/')[0];
            }

            // 종성 인덱스 계산 (0이면 받침 없음, 1 이상이면 받침 있음)
            int jongseongIndex = (lastChar - HANGUL_BASE) % 28;
            bool hasJongseong = jongseongIndex > 0;
            
            // 'ㄹ' 받침 예외 처리 (인덱스 8)
            bool isRieul = jongseongIndex == 8;

            string[] particles = particleFormat.Split('/');
            if (particles.Length != 2) return word + particleFormat; // 포맷이 잘못된 경우 그대로 반환

            string particle = "";

            // '으로/로' 예외 처리 (받침이 'ㄹ'인 경우 받침이 없는 것과 동일하게 '로'가 붙음)
            if (particleFormat == "으로/로")
            {
                particle = (hasJongseong && !isRieul) ? particles[0] : particles[1];
            }
            else
            {
                particle = hasJongseong ? particles[0] : particles[1];
            }

            return word + particle;
        }
    }
}