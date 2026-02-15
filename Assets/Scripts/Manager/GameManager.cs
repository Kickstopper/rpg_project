using System.Collections.Generic;
using UnityEngine;
using Data;

namespace Manager
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // 부모가 있다면 관계를 끊고 최상위로 나옴.
                transform.SetParent(null);
            
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

    }

}
