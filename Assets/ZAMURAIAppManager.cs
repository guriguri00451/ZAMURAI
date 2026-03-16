using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ZAMURAI
{
    public class ZAMURAIAppManager : MonoBehaviour
    {
        public static ZAMURAIAppManager Instance { get; private set; }
        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                Instance = this;
                DontDestroyOnLoad(this.gameObject);
            }
        }


        private string roomName = "ZAMURAI_Main";
        public string RoomName => roomName;
    }
}
