using RPGProject.Feature.Battle;

namespace RPGProject.Feature.Negotiation
{
    public static class NegotiationCalculator
    {
        public static MoodDelta CalculateMoodChange(ChoiceTone choice, MonsterController monster, EnvironmentState env)
        {
            if (monster == null || monster.sourceData == null) return new MoodDelta(0, 0, 0);
            return CalculateMoodChange(choice, monster.sourceData.personality, monster.sourceData.race, env);
        }

        public static MoodDelta CalculateMoodChange(ChoiceTone choice, Personality personality, Race race, EnvironmentState env)
        {
            int anger = 0, joy = 0, interest = 0;
            // Acceptance and offering money are not proof of payment.
            if (choice == ChoiceTone.Bribe || choice == ChoiceTone.Accept || choice == ChoiceTone.Refuse)
                return new MoodDelta(0, 0, 0);
            switch (choice)
            {
                case ChoiceTone.Gentle: joy = 25; interest = 15; break;
                case ChoiceTone.Relieve: anger = -15; joy = 20; interest = 20; break;
                case ChoiceTone.Persuade: joy = 20; interest = 30; break;
                case ChoiceTone.Request: joy = 5; interest = 15; break;
                case ChoiceTone.Threat: anger = 20; interest = 15; break;
                case ChoiceTone.Flirt: joy = 15; interest = 20; break;
                case ChoiceTone.Insult: anger = 40; joy = -20; break;
                case ChoiceTone.Mad: anger = 35; interest = -10; break;
            }
            switch (personality)
            {
                case Personality.Polite:
                    if (choice == ChoiceTone.Gentle || choice == ChoiceTone.Persuade) joy += 15;
                    if (choice == ChoiceTone.Threat || choice == ChoiceTone.Insult) anger += 20;
                    break;
                case Personality.Aggressive:
                    if (choice == ChoiceTone.Threat) { anger = 0; joy += 20; interest += 25; }
                    if (choice == ChoiceTone.Gentle) { anger += 20; joy = 5; }
                    break;
                case Personality.Sly:
                    if (choice == ChoiceTone.Persuade || choice == ChoiceTone.Flirt) interest += 20;
                    break;
                case Personality.Foolish:
                    if (choice == ChoiceTone.Relieve) joy += 20;
                    if (choice == ChoiceTone.Insult) anger += 15;
                    break;
                case Personality.Childish:
                    if (choice == ChoiceTone.Gentle || choice == ChoiceTone.Relieve) joy += 20;
                    if (choice == ChoiceTone.Threat) anger += 20;
                    break;
            }
            if (env.moonPhase == MoonPhase.Full) { anger += 15; joy -= 10; }
            if ((env.weather == Weather.Rain || env.weather == Weather.Storm) && race == Race.Beast) anger += 10;
            return new MoodDelta(anger, joy, interest);
        }
    }
}
