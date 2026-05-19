namespace Data
{
    public class PartyID { 
        public const string CHARACTER_00 = "chr_00";
        public const string CHARACTER_01 = "chr_01";
        public const string CHARACTER_02 = "chr_02";
        public const string CHARACTER_03 = "chr_03";
        public const string CHARACTER_04 = "chr_04";
        public const string CHARACTER_05 = "chr_05";
        public const string CHARACTER_06 = "chr_06"; 
    }

    [System.Serializable]
    public static class DefaultCharacterData
    {
        public static RuntimeCharacterData GetDefaultCharacterData(string partyId)
        {
            switch(partyId)
            {
                case PartyID.CHARACTER_00:
                case PartyID.CHARACTER_01:
                case PartyID.CHARACTER_02:
                case PartyID.CHARACTER_03:
                case PartyID.CHARACTER_04:
                case PartyID.CHARACTER_05:
                case PartyID.CHARACTER_06:
                return GetInitialData(partyId);

                default:
                return null;
            }
        }

        private static RuntimeCharacterData GetInitialData(string partyId)
        {
            // 임시
            var data = new RuntimeCharacterData();
            data.characterId = partyId;
            data.name = "---";
            data.isCommander = partyId == PartyID.CHARACTER_00;
            data.align = Align.True_Neutral;
            data.isRegular = true;
            data.row = RowType.Front;
            data.resistances = new ResistanceData();
            data.stats = new StatData(){ level = 1, str = 5, vit = 5, mag = 5, agi = 5, intel = 5, luc = 5};
            data.currentHp = data.maxHp = 0; 
            data.currentMp = data.maxMp = 0;
            return data;
        }

    }
}