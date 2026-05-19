using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UI.Battle;
using Manager;
using UnityEngine.UI;
using Data;

namespace UI.CharacterCreationScene
{
    public enum CreationStep { PlayerName, PartnerName, PlayerStats, PartnerStats, Done }

    public class CharacterCreationManager : MonoBehaviour
    {
        [Header("Next Scene Info")]
        public string nextSceneName;
        public string nextSceneParam;

        [Header("UI Panels")]
        public GameObject nameInputPanel;
        public LevelUpUI levelUpUI;
        
        [Header("Name Input Elements")]
        public Image portraitImage;
        public TextMeshProUGUI defaultNameText;

        public TextMeshProUGUI titleText;
        public TMP_InputField nameInputField;
        public VirtualKeyboard virtualKeyboard;

        private CreationStep currentStep = CreationStep.PlayerName;
        
        // 저장될 캐릭터 데이터
        private StatData playerStats;
        private StatData partnerStats;

        private void Start()
        {
            UpdateStep(CreationStep.PlayerName);
        }

        private void UpdateStep(CreationStep nextStep)
        {
            currentStep = nextStep;
            
            switch (currentStep)
            {
                case CreationStep.PlayerName:
                    levelUpUI.LeveUpUI.SetActive(false);
                    titleText.text = "당신의 이름을 입력하세요";

                    nameInputPanel.SetActive(true);
                    virtualKeyboard.ClearInput();
                    SetDefaultCharacterInfo(PartyID.CHARACTER_00);
                    break;

                case CreationStep.PartnerName:
                    titleText.text = "소중한 파트너의 이름을 입력하세요";

                    nameInputPanel.SetActive(true);
                    virtualKeyboard.ClearInput();
                    SetDefaultCharacterInfo(PartyID.CHARACTER_01);
                    break;

                case CreationStep.PlayerStats:
                    nameInputPanel.SetActive(false);
                    levelUpUI.LeveUpUI.SetActive(true);
                    // 스탯 보너스 포인트 15
                    levelUpUI.ShowForCreation(PartyID.CHARACTER_00, 15, OnPlayerStatsConfirmed);
                    break;

                case CreationStep.PartnerStats:
                    SetCharacterStats(PartyID.CHARACTER_00, playerStats); // 플레이어 스탯 저장
                    levelUpUI.ShowForCreation(PartyID.CHARACTER_01, 15, OnPartnerStatsConfirmed);
                    break;

                case CreationStep.Done:
                    SetCharacterStats(PartyID.CHARACTER_01, partnerStats); // 히로인 스탯 저장
                    TransitionToNextScene();
                    break;
            }
        }

        public void ChangePlaceholder(string newText)
        {
            // placeholder 속성을 TextMeshProUGUI로 형변환
            TextMeshProUGUI placeholder = nameInputField.placeholder as TextMeshProUGUI;
            
            if (placeholder != null)
                placeholder.text = newText;
        }

        private void SetDefaultCharacterInfo(string characterId)
        {
            var dbEntry = PartyManager.Instance.charDB.GetEntry(characterId);
            if (dbEntry != null)
            {
                portraitImage.sprite = dbEntry.portraitImage;
                defaultNameText.text = dbEntry.name;
                ChangePlaceholder(dbEntry.name);
            }
        }

        public void OnNameConfirmClicked()
        {
            string finalName = nameInputField.text;

            if (string.IsNullOrWhiteSpace(finalName))
            {
                TextMeshProUGUI placeholderText = nameInputField.placeholder as TextMeshProUGUI;
                
                if (placeholderText != null && !string.IsNullOrWhiteSpace(placeholderText.text))
                {
                    // Placeholder의 텍스트를 최종 이름으로 확정
                    finalName = placeholderText.text.Trim();
                }
                else
                {
                    // 예외 처리
                    if (currentStep == CreationStep.PlayerName)
                        finalName = "HERO";
                    else
                        finalName = "HEROINE";
                }
            }

            // finalName을 사용해 다음 단계로 진행
            if (currentStep == CreationStep.PlayerName)
            {
                SetCharacterName(PartyID.CHARACTER_00, finalName);
                UpdateStep(CreationStep.PartnerName);
            }
            else if (currentStep == CreationStep.PartnerName)
            {
                SetCharacterName(PartyID.CHARACTER_01, finalName);
                UpdateStep(CreationStep.PlayerStats);
            }
        }

        private void OnPlayerStatsConfirmed(StatData finalStats)
        {
            playerStats = finalStats;
            UpdateStep(CreationStep.PartnerStats);
        }

        private void OnPartnerStatsConfirmed(StatData finalStats)
        {
            partnerStats = finalStats;
            UpdateStep(CreationStep.Done);
        }

        private StatData CreateBaseStat(int baseValue)
        {
            return new StatData
            {
                str = baseValue, mag = baseValue, intel = baseValue,
                vit = baseValue, agi = baseValue, luc = baseValue
            };
        }

        private void SetCharacterName(string characterId, string name)
        {
            var characterData = PartyManager.Instance.GetCharacterByID(characterId);
            if (characterData != null) characterData.name = name;
        }

        private void SetCharacterStats(string characterId, StatData stats)
        {
            var characterData = PartyManager.Instance.GetCharacterByID(characterId);
            if (characterData != null) characterData.stats = stats;
        }

        private void TransitionToNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (nextSceneName.Equals(GameScene.DUNGEON_MAP_SCENE))
                {
                    DungeonManager.Instance.LoadDungeonFromJson(nextSceneParam, () => {
                        SceneManager.LoadScene(GameScene.DUNGEON_MAP_SCENE);
                    });
                }
                else if (nextSceneName.Equals(GameScene.WORLD_MAP_SCENE))
                {
                    WorldManager.Instance.SetCurrentRegionTheme(nextSceneParam); 
                    SceneManager.LoadScene(GameScene.WORLD_MAP_SCENE);
                }
                else
                {
                    SceneManager.LoadScene(nextSceneName);
                }
            }
            else
            {
                Debug.LogWarning("전환할 씬의 이름이 입력되지 않았습니다!");
            }
        }
    }
}