using UnityEngine;

namespace HotPotato
{
    public sealed class PotatoActor
    {
        public readonly int Index;
        public readonly string Name;
        public readonly Color Color;
        public readonly Transform Root;
        public readonly Transform Body;
        public readonly Transform Ring;
        public readonly Transform Marker;
        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Facing = Vector3.back;
        public Vector3 DashDirection;
        public Vector3 Wander;
        public bool Alive = true;
        public float DashTime;
        public float Cooldown;
        public float BrainTime;
        public float Bounce;
        public float VerticalSpeed;
        public float CoyoteTime;
        public float JumpBuffer;
        public float EscapeTime;
        public float Recovery;
        public float ChargeTime;
        public float BotChargeGoal;
        public bool Grounded = true;
        public bool TagArmed;
        public Vector3 ThrowDirection;
        readonly Transform shadow;
        readonly Material ringMaterial;
        readonly Material safeMaterial;
        readonly Transform silhouette;
        readonly PotatoView view;
        public int Target = -1;
        readonly Transform leftFoot;
        readonly Transform rightFoot;

        public PotatoActor(int index, string name, Color color, Transform parent, PotatoView view)
        {
            Index = index;
            this.view = view;
            Name = name;
            Color = color;
            Root = new GameObject(name).transform;
            Root.SetParent(parent, false);
            Body = new GameObject("Squash and turn").transform;
            Body.SetParent(Root, false);
            var clay = view.Material(color);
            var ink = view.Material(PotatoView.Ink);
            var white = view.Material(new Color(1f, 0.98f, 0.89f));
            view.Shape("Bean", PrimitiveType.Capsule, Body, new Vector3(0, 0.75f, 0), new Vector3(0.72f, 0.57f, 0.7f), clay);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                view.Shape("Eye white", PrimitiveType.Sphere, Body, new Vector3(sign * 0.16f, 0.9f, 0.30f), new Vector3(0.23f, 0.27f, 0.11f), white);
                view.Shape("Pupil", PrimitiveType.Sphere, Body, new Vector3(sign * 0.16f, 0.9f, 0.36f), new Vector3(0.105f, 0.14f, 0.055f), ink);
                view.Shape("Arm", PrimitiveType.Sphere, Body, new Vector3(sign * 0.4f, 0.63f, 0.02f), new Vector3(0.23f, 0.38f, 0.24f), clay);
            }
            leftFoot = view.Shape("Left foot", PrimitiveType.Sphere, Body, new Vector3(-0.2f, 0.18f, 0.08f), new Vector3(0.28f, 0.24f, 0.4f), ink);
            rightFoot = view.Shape("Right foot", PrimitiveType.Sphere, Body, new Vector3(0.2f, 0.18f, 0.08f), new Vector3(0.28f, 0.24f, 0.4f), ink);
            shadow = view.Shape("Soft shadow", PrimitiveType.Cylinder, Root, new Vector3(0, 0.015f, 0), new Vector3(0.98f, 0.008f, 0.75f), view.Material(new Color(0.26f, 0.47f, 0.49f)));
            ringMaterial = view.Material(PotatoView.Coral);
            safeMaterial = white;
            Ring = view.Ring("Holder ring", Root, 0.64f, 0.075f, ringMaterial);
            Ring.localPosition = Vector3.up * 0.04f;
            Marker = view.Shape("You marker", PrimitiveType.Cube, Root, new Vector3(0, 2f, 0), Vector3.one * 0.18f, white);
            Marker.localRotation = Quaternion.Euler(0, 0, 45);
            Marker.gameObject.SetActive(index == 0);
            silhouette = view.Shape("Occluded character", PrimitiveType.Capsule, Root, new Vector3(0, 0.75f, 0), new Vector3(0.72f, 0.57f, 0.7f), view.SilhouetteMaterial(color));
            silhouette.gameObject.SetActive(false);
        }

        public void Animate(float dt, float clock, bool holder, bool protectedFromReturn = false)
        {
            Root.position = Position;
            if (!Alive) return;
            silhouette.gameObject.SetActive(view.Occluded(Position + Vector3.up * 0.65f));
            float floor = ArenaGeometry.Floor(Position);
            shadow.position = new Vector3(Position.x, floor + 0.02f, Position.z);
            Ring.position = new Vector3(Position.x, floor + 0.04f, Position.z);
            float speed = Velocity.magnitude;
            float stride = Mathf.Sin(clock * 18f + Index) * Mathf.Clamp01(speed / 3f);
            Bounce = Mathf.MoveTowards(Bounce, 0, dt * 3f);
            Body.localPosition = new Vector3(0, Mathf.Abs(stride) * 0.09f + Bounce * 0.12f, 0);
            float squash = (DashTime > 0 ? -0.18f : stride * 0.035f) + Bounce * 0.18f;
            Body.localScale = new Vector3(1 - squash * 0.4f, 1 + squash, 1 - squash * 0.4f);
            if (Facing.sqrMagnitude > 0.01f)
                Body.rotation = Quaternion.Slerp(Body.rotation, Quaternion.LookRotation(Facing), 1 - Mathf.Exp(-18 * dt));
            leftFoot.localPosition = new Vector3(-0.2f, 0.18f + Mathf.Max(0, stride) * 0.12f, 0.08f + stride * 0.12f);
            rightFoot.localPosition = new Vector3(0.2f, 0.18f + Mathf.Max(0, -stride) * 0.12f, 0.08f - stride * 0.12f);
            Ring.gameObject.SetActive(holder || protectedFromReturn || Index == 0);
            Ring.GetComponent<MeshRenderer>().sharedMaterial = protectedFromReturn ? safeMaterial : ringMaterial;
            Ring.localScale = Vector3.one * (holder ? 1 + Mathf.Sin(clock * 12) * 0.06f : 0.8f);
            Marker.localPosition = new Vector3(0, 1.9f + Mathf.Sin(clock * 3) * 0.1f, 0);
        }
    }
}
