using UnityEngine;

namespace HotPotato
{
    // Shared by rendering, player movement, projectile collision and trajectory previews.
    public static class ArenaGeometry
    {
        public const float Height = 1.25f;
        public const float Gravity = 22;
        public const float ThrowSpeedScale = 1.2f;
        // Scale velocity and gravity together to traverse the same throw arc faster.
        public const float BombGravity = Gravity * ThrowSpeedScale * ThrowSpeedScale;
        public static readonly float[] PlatformX = { -3f, 3f };

        public static float Floor(Vector3 point)
        {
            if (Flat(point).magnitude > HotPotatoGame.ArenaRadius) return -100f;
            foreach (float x in PlatformX)
            {
                if (Mathf.Abs(point.x - x) > 1.1f) continue;
                if (point.z >= 0 && point.z <= 2.4f) return Height;
                if (point.z >= -2 && point.z < 0) return Height * (point.z + 2) / 2;
            }
            return 0;
        }

        public static Vector3 Clamp(Vector3 point, float radius = HotPotatoGame.ArenaRadius)
        {
            Vector3 flat = Vector3.ClampMagnitude(Flat(point), radius);
            return new Vector3(flat.x, point.y, flat.z);
        }

        public static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0, vector.z);

        public static Vector3 Clamp(Vector3 point)
        {
            Vector3 flat = Vector3.ClampMagnitude(Flat(point), HotPotatoGame.ArenaRadius);
            return new Vector3(flat.x, point.y, flat.z);
        }

        public static Vector3 ThrowVelocity(Vector3 direction, float charge)
        {
            return (Flat(direction).normalized * Mathf.Lerp(7.5f, 10.5f, charge) + Vector3.up * Mathf.Lerp(-1.2f, 8f, charge)) * ThrowSpeedScale;
        }

        public static Vector3 Arc(Vector3 start, Vector3 velocity, float time)
        {
            return start + velocity * time + Vector3.down * (BombGravity * time * time * 0.5f);
        }
    }
}
