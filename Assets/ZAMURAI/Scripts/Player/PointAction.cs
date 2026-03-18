using UnityEngine;
using Fusion;

namespace ZAMURAI.Player
{
    public struct PointAction : INetworkStruct
    {
        public PointActionType Type;
        public int PlayerId;
    }


    public enum PointActionType
    {
        tuntun,
        otuntun,
        samurai,
        tuntunsamurai,
        biron,
        syakin,
    }

}