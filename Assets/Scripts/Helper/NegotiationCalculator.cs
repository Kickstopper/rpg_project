using UnityEngine;
using Data;
using UI.Battle;

namespace Helper
{
    public static class NegotiationCalculator
    {
        public static MoodDelta CalculateMoodChange(ChoiceTone choice, MonsterController monster, EnvironmentState env)
        {
            int anger = 0;
            int joy = 0;
            int interest = 0;

            Personality personality = monster.sourceData.personality;
            Race species = monster.sourceData.race;

            // 성격에 따른 기본 반응 계산
            switch (personality)
            {
                case Personality.Aggressive: // 다혈질
                    if (choice == ChoiceTone.Gentle) anger += 20;    // 친절하게 대하면 얕봄
                    if (choice == ChoiceTone.Threat) interest += 30; // 같이 화내면 마음에 들어함
                    break;

                case Personality.Foolish: // 우둔함
                    if (choice == ChoiceTone.Bribe) joy += 50;         // 돈/아이템을 가장 좋아함
                    if (choice == ChoiceTone.Insult) anger += 30;     // 논리적으로 따지면 화를 냄
                    break;
            }

            // 달의 위상(Moon Phase) 보정
            if (env.moonPhase == MoonPhase.Full)
            {
                // 보름달에는 악마들이 흥분 상태라 대화가 잘 안 통함 (모든 수치 분노로 변환)
                anger += 50; 
                joy -= 20;
            }

            // 종족과 날씨 시너지
            if (env.weather == Weather.Rain && species == Race.Beast)
            {
                // 비가 올 때 야수형 악마는 신경질적임
                anger += 15;
            }

            // 최종 계산된 변화량을 구조체에 담아 반환
            return new MoodDelta(anger, joy, interest);
        }
    }
}