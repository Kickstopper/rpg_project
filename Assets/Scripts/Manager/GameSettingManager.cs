using UnityEngine;

namespace Manager
{
    public class GameSettingManager : MonoBehaviour
    {
        public bool useAnaglyph = false;

        public int DeviceRentalFee { get; private set; } = 5000;

    }

}
