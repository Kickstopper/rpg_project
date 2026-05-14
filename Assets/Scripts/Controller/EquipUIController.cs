using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Manager;
using Data;
using System.Linq;
using UI.Common;
using UnityEngine.EventSystems;

namespace Controller
{
    public enum EquipSlotType { Melee, Gun, Ammo, Head, Body, Hands, Feet, Acc }

    public class EquipUIController : MonoBehaviour
    {
        [Header("Name Texts")]
        public TextMeshProUGUI nameText;
        [Header("Slot Buttons")]
        public EquipSlotUI[] equipSlots; 

        [Header("Item List (Popup)")]
        public GameObject itemListPanel;  
        public Transform itemContent; 
        public ScrollRect itemScrollRect;
        public GameObject itemSlotPrefab; 

        [Header("Info Windows")]
        public TextMeshProUGUI itemInfoText;  
        
        [Header("Stat Texts")]
        public TextMeshProUGUI atkText;
        public TextMeshProUGUI hitText;
        public TextMeshProUGUI gunText;
        public TextMeshProUGUI gunHitText; 
        public TextMeshProUGUI magPowText; 
        public TextMeshProUGUI magFxText;  
        public TextMeshProUGUI defText;
        public TextMeshProUGUI evaText;
        
        public System.Action onCloseCallback;

        private RuntimeCharacterData currentCharacter;
        private int currentSlotIndex = 0;
        
        private bool isSelectingItem = false;
        private int currentItemIndex = 0;
        private List<Button> displayedButtons = new List<Button>(); 
        private List<string> displayedItemIds = new List<string>(); 

        private List<RuntimeCharacterData> partyMembers;
        private int currentIndex = 0;

        private float inputCooldown = 0f;
        
        public void SetCharacter(RuntimeCharacterData character)
        {
            if (PartyManager.Instance == null) return;
            partyMembers = PartyManager.Instance.partyData;

            // 전달받은 캐릭터가 파티 리스트의 몇 번째 인덱스인지 찾음
            int foundIndex = partyMembers.IndexOf(character);
            
            if (foundIndex != -1)
            {
                currentIndex = foundIndex;
                currentCharacter = character;
                RefreshUI();
            }
        }

        private void RefreshUI()
        {
            nameText.text = currentCharacter.name;
            isSelectingItem = false;
            itemListPanel.SetActive(false);
            currentSlotIndex = 0;
            inputCooldown = 0f;

            RefreshButtonText();
            UpdateStatDisplay();
            
            SelectSlot(0);
        }

        void Update()
        {
            if (inputCooldown > 0)
            {
                inputCooldown -= Time.deltaTime;
                return;
            }

            if (isSelectingItem)
                HandleItemListInput();
            else
                HandleSlotInput();
        }

        private void ChangeCharacter(int direction)
        {
            if (partyMembers == null || partyMembers.Count == 0) return;

            currentIndex += direction;

            // 리스트 순환 (Loop)
            if (currentIndex < 0) currentIndex = partyMembers.Count - 1;
            else if (currentIndex >= partyMembers.Count) currentIndex = 0;

            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);

            SetCharacter(partyMembers[currentIndex]);
        }

        private void HandleSlotInput()
        {
            if (!isSelectingItem)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                {
                    ChangeCharacter(-1);
                }
                else if (Input.GetKeyDown(KeyCode.E))
                {
                    ChangeCharacter(1);
                }
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) MoveSlotCursor(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) MoveSlotCursor(1);

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                OpenItemList(equipSlots[currentSlotIndex].type);
                inputCooldown = 0.2f;
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                if (onCloseCallback != null)
                {
                    // 외부에서 덮어씌운 콜백이 있다면 그것을 실행
                    onCloseCallback.Invoke();
                }
            }
        }

        private void MoveSlotCursor(int dir)
        {
            currentSlotIndex = (currentSlotIndex + dir + equipSlots.Length) % equipSlots.Length;
            SelectSlot(currentSlotIndex);
        }

        private void SelectSlot(int index)
        {
            equipSlots[index].button.Select();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            string itemId = GetEquippedId(equipSlots[index].type);
            UpdateItemInfoText(itemId);
        }

        private void RefreshButtonText()
        {
            for (int i = 0; i < equipSlots.Length; i++)
            {
                string itemId = GetEquippedId(equipSlots[i].type);
                string itemName = "EMPTY";
                BaseRootData itemData = DatabaseManager.Instance.GetItem(itemId);
                if (itemData != null) itemName = itemData.dataName;
                equipSlots[i].UpdateText(itemName);
                int slotIndex = i;
                Button btn = equipSlots[i].button;
                btn.onClick.RemoveAllListeners(); 
                btn.onClick.AddListener(() =>
                {
                    OpenItemList(equipSlots[slotIndex].type);
                    inputCooldown = 0.2f;
                });

                EventTrigger trigger = btn.gameObject.GetComponent<EventTrigger>() ?? btn.gameObject.AddComponent<EventTrigger>();
                trigger.triggers.Clear();

                EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enterEntry.callback.AddListener((data) =>
                {
                    currentSlotIndex = slotIndex;
                    SelectSlot(slotIndex);
                });
                trigger.triggers.Add(enterEntry);
            }
        }

        private string GetEquippedId(EquipSlotType type)
        {
            switch (type)
            {
                case EquipSlotType.Melee: return currentCharacter.equippedWeaponId;
                case EquipSlotType.Gun: return currentCharacter.equippedGunId;
                case EquipSlotType.Ammo: return currentCharacter.equippedAmmoId;
                case EquipSlotType.Head: return GetArmorIdBySlot(ArmorSlot.Helmet);
                case EquipSlotType.Body: return GetArmorIdBySlot(ArmorSlot.Body);
                case EquipSlotType.Hands: return GetArmorIdBySlot(ArmorSlot.Gloves);
                case EquipSlotType.Feet: return GetArmorIdBySlot(ArmorSlot.Boots);
                case EquipSlotType.Acc: return GetArmorIdBySlot(ArmorSlot.Accessory);
                default: return "";
            }
        }

        private string GetArmorIdBySlot(ArmorSlot slotType)
        {
            foreach (string id in currentCharacter.equippedArmorIds)
            {
                ArmorData armor = DatabaseManager.Instance.GetArmor(id);
                if (armor != null && armor.slot == slotType) return id;
            }
            return "";
        }

        private void UpdateStatDisplay()
        {
            int str = currentCharacter.stats.str;
            int vit = currentCharacter.stats.vit;
            int mag = currentCharacter.stats.mag;
            int agi = currentCharacter.stats.agi;
            int luc = currentCharacter.stats.luc;
            int intel = currentCharacter.stats.intel;
            int lv = currentCharacter.stats.level;

            WeaponData weapon = DatabaseManager.Instance.GetWeapon(currentCharacter.equippedWeaponId);
            WeaponData gun = DatabaseManager.Instance.GetWeapon(currentCharacter.equippedGunId);
            AmmoData ammo = DatabaseManager.Instance.GetAmmo(currentCharacter.equippedAmmoId);
            
            int armorDef = 0;
            int armorEva = 0;
            foreach(var id in currentCharacter.equippedArmorIds)
            {
                var a = DatabaseManager.Instance.GetArmor(id);
                if(a) { armorDef += a.defense; armorEva += a.evasionMod; }
            }

            // 기획서 공식 적용
            int atk = str + (weapon != null ? weapon.attackPower : 0) + (lv / 4);
            int hit = agi + (weapon != null ? weapon.hitRateBonus : 0) + (luc / 2) + lv;
            
            int gunAtk = 0;
            int gunHit = 0;
            if (gun != null && ammo != null)
            {
                gunAtk = gun.attackPower + ammo.damageBonus + (lv / 4);
                gunHit = gun.hitRateBonus + ammo.hitRateBonus + agi + (luc / 2) + lv;
            }

            int def = armorDef + vit + agi;
            int eva = armorEva + agi + (intel / 4) + (luc / 4) + lv;

            int magPow = (mag * 2) + (intel / 2); // MATK
            int magFx = ((mag + vit + agi) / 4) + intel + (armorDef / 4); // MDEF

            // UI 텍스트 반영
            atkText.text = atk.ToString();
            hitText.text = hit.ToString();
            gunText.text = gunAtk.ToString();
            gunHitText.text = gunHit.ToString();
            magPowText.text = magPow.ToString();
            magFxText.text = magFx.ToString(); 
            defText.text = def.ToString();
            evaText.text = eva.ToString();
        }

        // 아이템 리스트 표시
        private void OpenItemList(EquipSlotType slotType)
        {
            isSelectingItem = true;
            itemListPanel.SetActive(true);
            
            foreach (Transform child in itemContent) Destroy(child.gameObject);
            itemContent.DetachChildren(); 
            
            displayedButtons.Clear();
            displayedItemIds.Clear();

            CreateListItem(slotType, "", "REMOVE", 0);

            List<string> uniqueItemIds = InventoryManager.Instance.GetAllItemIds().Distinct().ToList();

            foreach (string itemId in uniqueItemIds)
            {
                if (!IsItemMatchSlot(itemId, slotType)) continue;

                int count = InventoryManager.Instance.GetItemCount(itemId);
                if (count > 0)
                {
                    BaseRootData data = DatabaseManager.Instance.GetItem(itemId);
                    if (data != null)
                    {
                        CreateListItem(slotType, itemId, data.dataName, count);
                    }
                }
            }
            
            currentItemIndex = 0;
            if (displayedButtons.Count > 0)
            {
                UpdateItemSelection();
            }
        }

        private void CreateListItem(EquipSlotType slotType, string itemId, string text, int count)
        {
            GameObject go = Instantiate(itemSlotPrefab, itemContent);
            var btn = go.GetComponent<Button>();
            var slotUI = go.GetComponent<SimpleListItemView>();
            
            slotUI.SetData(text, count);

            int itemIndex = displayedButtons.Count;
            btn.onClick.AddListener(() => OnItemClicked(itemId, slotType));
            EventTrigger trigger = go.GetComponent<EventTrigger>() ?? go.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enterEntry.callback.AddListener((data) => {
                if (currentItemIndex != itemIndex)
                {
                    currentItemIndex = itemIndex;
                    SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
                    UpdateItemSelection(); // 포커스 변경 및 정보창 갱신
                }
            });
            trigger.triggers.Add(enterEntry);

            displayedButtons.Add(btn);
            displayedItemIds.Add(itemId);
        }

        private bool IsItemMatchSlot(string itemId, EquipSlotType slotType)
        {
             if (string.IsNullOrEmpty(itemId)) return false;
            
            WeaponData w = DatabaseManager.Instance.GetWeapon(itemId);
            if (w != null)
            {
                if (slotType == EquipSlotType.Melee && w.type == WeaponType.Melee) return true;
                if (slotType == EquipSlotType.Gun && w.type == WeaponType.Gun) return true;
                return false;
            }
            AmmoData am = DatabaseManager.Instance.GetAmmo(itemId);
            if (am != null) return slotType == EquipSlotType.Ammo;
            
            ArmorData ar = DatabaseManager.Instance.GetArmor(itemId);
            if (ar != null)
            {
                if (slotType == EquipSlotType.Head && ar.slot == ArmorSlot.Helmet) return true;
                if (slotType == EquipSlotType.Body && ar.slot == ArmorSlot.Body) return true;
                if (slotType == EquipSlotType.Hands && ar.slot == ArmorSlot.Gloves) return true;
                if (slotType == EquipSlotType.Feet && ar.slot == ArmorSlot.Boots) return true;
                if (slotType == EquipSlotType.Acc && ar.slot == ArmorSlot.Accessory) return true;
                return false;
            }
            return false;
        }

        // 아이템 선택 및 장착
        private void HandleItemListInput()
        {
            if (displayedButtons.Count == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                currentItemIndex--;
                if (currentItemIndex < 0) currentItemIndex = displayedButtons.Count - 1;
                UpdateItemSelection();
            }
            else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                currentItemIndex++;
                if (currentItemIndex >= displayedButtons.Count) currentItemIndex = 0;
                UpdateItemSelection();
            }

            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.Tab) || UI.Common.GameInput.GetCancelDown())
            {
                CloseItemList();
            }

        }

        private void UpdateItemSelection()
        {
            if (displayedButtons.Count == 0) return;
            displayedButtons[currentItemIndex].Select();
            SoundManager.Instance.PlaySFX(SfxID.UI_Cursor);
            
            string itemId = displayedItemIds[currentItemIndex];
            UpdateItemInfoText(itemId);

            if (itemScrollRect != null)
                SnapTo(displayedButtons[currentItemIndex].transform as RectTransform);
        }

        private void SnapTo(RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 targetLocalPosition = target.localPosition;
            float newY = -targetLocalPosition.y - (itemScrollRect.viewport.rect.height / 2);
            float maxY = itemContent.GetComponent<RectTransform>().rect.height - itemScrollRect.viewport.rect.height;
            newY = Mathf.Clamp(newY, 0, maxY > 0 ? maxY : 0);
            itemContent.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, newY);
        }

        private void OnItemClicked(string newItemId, EquipSlotType slotType)
        {
            // 쿨타임 중이면 무시 (이벤트 시스템 중복 호출 방지용 안전장치)
            if (inputCooldown > 0) return;

            SoundManager.Instance.PlaySFX(SfxID.UI_Click);

            // 기존 장착 아이템 해제 (인벤토리 복구)
            string oldItemId = GetEquippedId(slotType);
            if (!string.IsNullOrEmpty(oldItemId))
            {
                InventoryManager.Instance.AddItem(oldItemId, 1);
                UnequipItemFromMe(slotType, oldItemId);
            }

            // 새 아이템 장착 (인벤토리 차감)
            if (!string.IsNullOrEmpty(newItemId))
            {
                InventoryManager.Instance.UseItem(newItemId); 
                EquipItemToMe(slotType, newItemId);
            }

            CloseItemList();
            
            // 리스트 닫은 직후 쿨타임 설정 -> HandleSlotInput에서 다시 열리는 것 방지
            inputCooldown = 0.2f; 
            
            RefreshButtonText();
            UpdateStatDisplay();
        }

        private void CloseItemList()
        {
            isSelectingItem = false;
            itemListPanel.SetActive(false);
            
            // 닫을 때도 쿨타임을 주어 실수로 바로 다시 열거나 다른 조작 방지
            inputCooldown = 0.2f; 
            
            SelectSlot(currentSlotIndex); 
        }

        private void EquipItemToMe(EquipSlotType type, string id)
        {
            switch (type)
            {
                case EquipSlotType.Melee: currentCharacter.equippedWeaponId = id; break;
                case EquipSlotType.Gun: currentCharacter.equippedGunId = id; break;
                case EquipSlotType.Ammo: currentCharacter.equippedAmmoId = id; break;
                default: currentCharacter.equippedArmorIds.Add(id); break; 
            }
        }

        private void UnequipItemFromMe(EquipSlotType type, string id)
        {
            switch (type)
            {
                case EquipSlotType.Melee: currentCharacter.equippedWeaponId = ""; break;
                case EquipSlotType.Gun: currentCharacter.equippedGunId = ""; break;
                case EquipSlotType.Ammo: currentCharacter.equippedAmmoId = ""; break;
                default: currentCharacter.equippedArmorIds.Remove(id); break;
            }
        }

        private void UpdateItemInfoText(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                itemInfoText.text = "EMPTY";
                return;
            }
            BaseRootData data = DatabaseManager.Instance.GetItem(itemId);
            if (data != null)
            {
                string stats = "";
                if(data is WeaponData w) stats = $"ATK: {w.attackPower} HIT: {w.hitRateBonus}";
                else if(data is ArmorData a) stats = $"DEF: {a.defense} EVA: {a.evasionMod}";
                else if(data is AmmoData am) stats = $"DMG+: {am.damageBonus}";
                itemInfoText.text = $"{data.dataName}\n{stats}\n{data.description}";
            }
        }
    }

    [System.Serializable]
    public class EquipSlotUI
    {
        public Button button;
        public TextMeshProUGUI nameText;
        public EquipSlotType type;

        public void UpdateText(string text)
        {
            if (nameText) nameText.text = text;
        }
    }
}