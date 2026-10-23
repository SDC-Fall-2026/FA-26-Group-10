using UnityEngine;

namespace HotPotato
{
    // Policy outputs are intentions. Only the game can change possession or move actors.
    public struct PotatoBotCommand
    {
        public Vector2 Move;
        public Vector2 Aim;
        public bool Jump;
        public bool Lunge;
        public bool HoldThrow;
    }
}
