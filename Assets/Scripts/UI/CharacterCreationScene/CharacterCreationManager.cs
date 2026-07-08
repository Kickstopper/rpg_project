using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UI.Battle;
using Manager;
using UnityEngine.UI;
using Data;
using Helper;
using Unity.VisualScripting;

namespace UI.CharacterCreationScene
{
    public enum CreationStep { PlayerName, PartnerName, PlayerStats, PartnerStats, Done }

    public class CharacterCreationManager : MonoBehaviour
    {
        [Header("Next Scene Info")]
        public string nextSceneName;
        public string nextSceneParam;

        [Header("UI References")]
        public GameObject nameInputPanel;
        public GameObject levelUpPanel;

        public LevelUpUI statUIController;
        public VirtualKeyboard virtualKeyboard;
        
        [Header("Name Input Elements")]
        public Image portraitImage;
        public TextMeshProUGUI defaultNameText;

        public TextMeshProUGUI titleText;
        public TMP_InputField nameInputField;
        

        private CreationStep currentStep = CreationStep.PlayerName;

        void Start()
        {
            if (nameInputPanel != null) nameInputPanel.SetActive(false);
            if (levelUpPanel != null) levelUpPanel.SetActive(false);
        }

        public void StartFirstStep()
        {
            UpdateStep(CreationStep.PlayerName);
        }

        private void UpdateStep(CreationStep nextStep)
        {
            currentStep = nextStep;
            
            switch (currentStep)
            {
                case CreationStep.PlayerName:
                    if (SoundManager.Instance != null)
                        ManagerRoot.Sound.PlayBGM(BgmID.LevelUp);
                    titleText.text = "당신의 이름을 입력하세요";

                    nameInputPanel.SetActive(true);
                    virtualKeyboard.ClearInput();
                    virtualKeyboard.FocusFirstKey();
                    SetDefaultCharacterInfo(PartyID.CHARACTER_00);
                    break;

                case CreationStep.PartnerName:
                    titleText.text = "소중한 파트너의 이름을 입력하세요";

                    nameInputPanel.SetActive(true);
                    virtualKeyboard.ClearInput();
                    virtualKeyboard.FocusFirstKey();
                    SetDefaultCharacterInfo(PartyID.CHARACTER_01);
                    break;

                case CreationStep.PlayerStats:
                    if (SoundManager.Instance != null)
                        ManagerRoot.Sound.PlayBGM(BgmID.LevelUp);
                    nameInputPanel.SetActive(false);
                    levelUpPanel.SetActive(true);

                    SetCharacterStats(PartyID.CHARACTER_00, CreateBaseStat(5)); // 주인공 기본 스탯 저장
                    // 스탯 보너스 포인트 15
                    statUIController.ShowForCreation(PartyID.CHARACTER_00, 15, OnPlayerStatsConfirmed);
                    break;

                case CreationStep.PartnerStats:
                    if (SoundManager.Instance != null)
                    {
                        ManagerRoot.Sound.StopBGM(false);
                        ManagerRoot.Sound.PlayBGM(BgmID.LevelUp);
                    }
                    
                    SetCharacterStats(PartyID.CHARACTER_01, CreateBaseStat(5)); // 히로인 기본 스탯 저장
                    statUIController.ShowForCreation(PartyID.CHARACTER_01, 15, OnPartnerStatsConfirmed);
                    break;

                case CreationStep.Done:
                    TransitionToNextScene();
                    break;
            }
        }

        public void ChangePlaceholder(string newText)
        {
            TextMeshProUGUI placeholder = nameInputField.placeholder as TextMeshProUGUI;
            
            if (placeholder != null)
                placeholder.text = newText;
            
            // Placeholder 글자가 바뀌었으므로 가상 키보드 화면도 즉시 새로고침
            if (virtualKeyboard != null)
                virtualKeyboard.ForceUpdateDisplay();
        }

        private void SetDefaultCharacterInfo(string characterId)
        {
            var dbEntry = ManagerRoot.Database.charDB.GetEntry(characterId);
            if (dbEntry != null)
            {
                portraitImage.sprite = dbEntry.portraitImage;
                defaultNameText.text = dbEntry.name;
                ChangePlaceholder(dbEntry.name);
            }
        }

        public void OnCharacterSetToggleClicked()
        {
            if (virtualKeyboard != null)
            {
                virtualKeyboard.ToggleLanguage();
            }
            else
            {
                Debug.LogWarning("VirtualKeyboard 참조가 누락되었습니다.");
            }
        }

        public void OnCapsToggleClicked()
        {
            if (virtualKeyboard != null)
            {
                virtualKeyboard.ToggleCapsLock();
            }
            else
            {
                Debug.LogWarning("VirtualKeyboard 참조가 누락되었습니다.");
            }
        }

        public void OnNameConfirmClicked()
        {
            string finalName = virtualKeyboard.GetInputText();

            // 순수 입력 데이터가 비어있다면 Placeholder 텍스트를 최종 이름으로 사용
            if (string.IsNullOrWhiteSpace(finalName))
            {
                TextMeshProUGUI placeholderText = nameInputField.placeholder as TextMeshProUGUI;
                
                if (placeholderText != null && !string.IsNullOrWhiteSpace(placeholderText.text))
                {
                    finalName = placeholderText.text.Trim();
                }
                else
                {
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
            SetCharacterStats(PartyID.CHARACTER_00, finalStats); // 플레이어 최종 스탯 저장
            UpdateStep(CreationStep.PartnerStats);
        }

        private void OnPartnerStatsConfirmed(StatData finalStats)
        {
            SetCharacterStats(PartyID.CHARACTER_01, finalStats); // 히로인 최종 스탯 저장
            UpdateStep(CreationStep.Done);
        }

        private StatData CreateBaseStat(int baseValue)
        {
            return new StatData
            {
                level = 1,
                str = baseValue, mag = baseValue, intel = baseValue,
                vit = baseValue, agi = baseValue, luc = baseValue
            };
        }

        private void SetCharacterName(string characterId, string name)
        {
            var characterData  = ManagerRoot.Party.GetCharacterByID(characterId);
            if (characterData != null) characterData.name = name;
        }

        private void SetCharacterStats(string characterId, StatData stats)
        {
            var characterData  = ManagerRoot.Party.GetCharacterByID(characterId);
            if (characterData != null)
            {
                characterData.stats = stats;
                characterData.currentHp = characterData.maxHp = BattleCalculator.GetMaxHP(stats.level, stats.str, stats.vit);  
                characterData.currentMp = characterData.maxMp = BattleCalculator.GetMaxMP(stats.level, stats.mag, stats.intel);  
            } 
        }

        private void TransitionToNextScene()
        {
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                if (nextSceneName.Equals(GameScene.DUNGEON_MAP_SCENE))
                {
                    ManagerRoot.Dungeon.LoadDungeonFromJson(nextSceneParam);
                    SceneManager.LoadScene(GameScene.DUNGEON_MAP_SCENE);
                }
                else if (nextSceneName.Equals(GameScene.WORLD_MAP_SCENE))
                {
                    ManagerRoot.World.SetCurrentRegionTheme(nextSceneParam); 
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