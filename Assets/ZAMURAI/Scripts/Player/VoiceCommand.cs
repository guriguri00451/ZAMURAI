using UnityEngine;
using System;

namespace ZAMURAI.Player
{
    [CreateAssetMenu(menuName = "ScriptableObject/VoiceCommand")]
    public class VoiceCommand : ScriptableObject
    {
        [SerializeField] private PointActionCommand[] pointCommands;
        public PointActionCommand[] PointCommands 
        {
            get => pointCommands;
        }
    }

    [Serializable]
    public struct PointActionCommand
    {
        public PointActionType actionType;
        public string commandName;
    }
}
