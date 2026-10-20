using System.Collections.Generic;
using UnityEngine;

namespace HotPotato
{
    public sealed class PotatoView
    {
        public static readonly Color Ink = new Color(0.13f, 0.19f, 0.26f);
        public static readonly Color Coral = new Color(1f, 0.31f, 0.25f);
        public static readonly Color Cream = new Color(0.98f, 0.95f, 0.86f);
        public static readonly Color Mint = new Color(0.47f, 0.78f, 0.73f);
        public static readonly Color[] Palette =
        {
            new Color(1f, 0.75f, 0.23f), new Color(0.68f, 0.53f, 0.95f),
            new Color(0.98f, 0.44f, 0.55f), new Color(0.29f, 0.68f, 0.94f),
            new Color(0.65f, 0.84f, 0.35f), new Color(1f, 0.59f, 0.28f),
            new Color(0.39f, 0.84f, 0.76f), new Color(0.91f, 0.65f, 0.87f),
            new Color(0.62f, 0.72f, 1f), new Color(0.87f, 0.83f, 0.53f),
            new Color(0.95f, 0.57f, 0.37f), new Color(0.63f, 0.85f, 0.94f)
        };
        public Camera Camera { get; private set; }
        public Transform Potato { get; private set; }
        public Transform Aim { get; private set; }
        public bool Muted { get; set; }
        readonly Transform root;
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly Dictionary<Vector2, Mesh> ringMeshes = new Dictionary<Vector2, Mesh>();
        readonly AudioSource audio;
        readonly AudioClip passClip;
        readonly AudioClip popClip;
        readonly AudioClip tickClip;
        readonly AudioClip winClip;
        readonly Particle[] particles = new Particle[96];
        readonly Material potatoMaterial;
        int particleCursor;
        float shake;
        float cameraSize;
        Vector3 potatoPosition;
        Vector3 passFrom;
        float passFlight;
        Mesh arenaCylinder;
        readonly Transform[] arcDots = new Transform[24];
        readonly Transform flightShadow;

        sealed class Particle
        {
            public Transform Transform;
            public MeshRenderer Renderer;
            public Vector3 Velocity;
            public float Life;
            public float Duration;
            public float Size;
        }

        public PotatoView(Transform parent)
        {
            root = new GameObject("Arena and feedback").transform;
            root.SetParent(parent, false);
            var cameraObject = new GameObject("Arena camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root, false);
            Camera = cameraObject.GetComponent<Camera>();
            Camera.tag = "MainCamera";
            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Cream;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 60;
            Camera.transform.position = new Vector3(0, 17, -12);
            Camera.transform.LookAt(new Vector3(0, 0, 0.35f));
            FrameCamera(1);

            audio = cameraObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.volume = 0.32f;
            passClip = Tone("Pass", 620, 1200, 0.12f, false);
            popClip = Tone("Pop", 160, 45, 0.38f, true);
            tickClip = Tone("Tick", 1000, 850, 0.055f, false);
            winClip = Tone("Winner", 440, 880, 0.55f, false);

            Shape("Coral arena base", PrimitiveType.Cylinder, root, new Vector3(0, -0.42f, 0), new Vector3(13.1f, 0.33f, 13.1f), Material(Coral));
            Shape("Cream rim", PrimitiveType.Cylinder, root, new Vector3(0, -0.14f, 0), new Vector3(12.7f, 0.1f, 12.7f), Material(Cream));
            Shape("Mint playfield", PrimitiveType.Cylinder, root, new Vector3(0, -0.055f, 0), new Vector3(12.1f, 0.05f, 12.1f), Material(Mint));
            Ring("Boundary stripe", root, 5.64f, 0.045f, Material(new Color(0.77f, 0.9f, 0.79f))).localPosition = Vector3.up * 0.003f;
            Ring("Center circle", root, 1.3f, 0.035f, Material(new Color(0.65f, 0.85f, 0.76f))).localPosition = Vector3.up * 0.004f;
            var dotMaterial = Material(new Color(0.57f, 0.82f, 0.75f));
            for (int x = -5; x <= 5; x++)
                for (int z = -5; z <= 5; z++)
                    if (x * x + z * z < 29)
                        Shape("Floor dot", PrimitiveType.Cylinder, root, new Vector3(x, 0.003f, z), new Vector3(0.045f, 0.003f, 0.045f), dotMaterial);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                var pos = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 6.18f;
                pos.y = 0.03f;
                var bumper = Shape("Rim cushion", PrimitiveType.Sphere, root, pos, new Vector3(0.4f, 0.24f, 0.2f), Material(i % 3 == 0 ? Coral : Cream));
                bumper.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
            }

            BuildPlatforms();
            Potato = new GameObject("The hot potato").transform;
            Potato.SetParent(root, false);
            potatoMaterial = Material(new Color(0.68f, 0.35f, 0.14f));
            Shape("Potato", PrimitiveType.Sphere, Potato, Vector3.zero, new Vector3(0.6f, 0.47f, 0.45f), potatoMaterial);
            for (int i = 0; i < 5; i++)
                Shape("Potato speckle", PrimitiveType.Sphere, Potato, new Vector3((i % 3 - 1) * 0.13f, 0.07f * (i / 3), -0.205f), Vector3.one * 0.055f, Material(new Color(0.37f, 0.2f, 0.1f)));
            var fuse = Shape("Fuse", PrimitiveType.Capsule, Potato, new Vector3(0.15f, 0.29f, 0), new Vector3(0.05f, 0.13f, 0.05f), Material(Ink));
            fuse.localRotation = Quaternion.Euler(0, 0, -25);
            Shape("Spark", PrimitiveType.Sphere, Potato, new Vector3(0.21f, 0.41f, 0), Vector3.one * 0.14f, Material(new Color(1f, 0.83f, 0.3f)));
            Aim = Ring("Target reticle", root, 0.7f, 0.065f, Material(Cream));
            Aim.gameObject.SetActive(false);
            flightShadow = Ring("Loose bomb responsibility", root, 0.45f, 0.09f, Material(Coral));
            flightShadow.gameObject.SetActive(false);
            for (int i = 0; i < arcDots.Length; i++)
            {
                arcDots[i] = Shape("Throw preview", PrimitiveType.Sphere, root, Vector3.zero, Vector3.one * 0.09f, Material(Cream));
                arcDots[i].gameObject.SetActive(false);
            }

            for (int i = 0; i < particles.Length; i++)
            {
                var bit = Shape("Pooled confetti", PrimitiveType.Cube, root, Vector3.zero, Vector3.one * 0.1f, Material(Palette[i % Palette.Length]));
                particles[i] = new Particle { Transform = bit, Renderer = bit.GetComponent<MeshRenderer>() };
                bit.gameObject.SetActive(false);
            }
        }

        public Material Material(Color color)
        {
            if (materials.TryGetValue(color, out var existing)) return existing;
            var shader = Resources.Load<Shader>("HotPotato/Clay");
            var material = new Material(shader) { color = color, name = "Clay " + ColorUtility.ToHtmlStringRGB(color) };
            materials.Add(color, material);
            return material;
        }

        public Material SilhouetteMaterial(Color color)
        {
            color.a = 0.55f;
            if (materials.TryGetValue(color, out var existing)) return existing;
            var material = new Material(Resources.Load<Shader>("HotPotato/Silhouette")) { color = color };
            materials.Add(color, material);
            return material;
        }

        public bool Occluded(Vector3 point)
        {
            Vector3 towardCamera = (Camera.transform.position - point).normalized;
            for (float distance = 0.15f; distance < 4; distance += 0.15f)
            {
                Vector3 sample = point + towardCamera * distance;
                if (sample.y < ArenaGeometry.Floor(sample)) return true;
            }
            return false;
        }

        public Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            if (type == PrimitiveType.Cylinder && scale.x > 8)
            {
                if (arenaCylinder == null) arenaCylinder = CreateArenaCylinder();
                obj.GetComponent<MeshFilter>().sharedMesh = arenaCylinder;
            }
            Object.Destroy(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        public Transform Ring(string name, Transform parent, float radius, float thickness, Material material)
        {
            Vector2 key = new Vector2(radius, thickness);
            if (!ringMeshes.TryGetValue(key, out Mesh mesh))
            {
                const int segments = 96;
                var vertices = new Vector3[segments * 2];
                var normals = new Vector3[vertices.Length];
                var triangles = new int[segments * 6];
                for (int i = 0; i < segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    Vector3 v = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    vertices[i * 2] = v * (radius - thickness);
                    vertices[i * 2 + 1] = v * radius;
                    normals[i * 2] = normals[i * 2 + 1] = Vector3.up;
                    int n = (i + 1) % segments * 2;
                    int t = i * 6;
                    triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = n;
                    triangles[t + 3] = i * 2 + 1; triangles[t + 4] = n + 1; triangles[t + 5] = n;
                }
                mesh = new Mesh { name = name, vertices = vertices, normals = normals, triangles = triangles };
                mesh.RecalculateBounds();
                meshes.Add(mesh);
                ringMeshes.Add(key, mesh);
            }
            var obj = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(parent, false);
            obj.GetComponent<MeshFilter>().sharedMesh = mesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = material;
            return obj.transform;
        }

        public void FrameCamera(float dt)
        {
            // Keep the entire arena visible on both landscape and portrait displays.
            float target = Mathf.Max(9.6f, 7.2f / Mathf.Max(0.3f, Camera.aspect));
            cameraSize = cameraSize <= 0 ? target : Mathf.Lerp(cameraSize, target, 1 - Mathf.Exp(-6 * dt));
            Camera.orthographicSize = cameraSize;
        }

        public Vector3 GroundPoint(Vector2 screen)
        {
            Ray ray = Camera.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (!plane.Raycast(ray, out float enter)) return Vector3.zero;
            float nearest = enter;
            foreach (float x in ArenaGeometry.PlatformX)
            {
                var top = new Plane(Vector3.up, Vector3.up * ArenaGeometry.Height);
                if (top.Raycast(ray, out float t) && t < nearest)
                {
                    var point = ray.GetPoint(t);
                    if (Mathf.Abs(point.x - x) <= 1.1f && point.z >= 0 && point.z <= 2.4f) nearest = t;
                }
                var ramp = new Plane(new Vector3(0, 1, -ArenaGeometry.Height / 2), new Vector3(x, 0, -2));
                if (ramp.Raycast(ray, out t) && t < nearest)
                {
                    var point = ray.GetPoint(t);
                    if (Mathf.Abs(point.x - x) <= 1.1f && point.z >= -2 && point.z <= 0) nearest = t;
                }
            }
            return ray.GetPoint(nearest);
        }

        void BuildPlatforms()
        {
            var platform = Material(new Color(0.43f, 0.65f, 0.73f));
            foreach (float x in ArenaGeometry.PlatformX)
            {
                Shape("Upper deck", PrimitiveType.Cube, root, new Vector3(x, ArenaGeometry.Height / 2, 1.2f), new Vector3(2.2f, ArenaGeometry.Height, 2.4f), platform);
                Shape("Deck edge", PrimitiveType.Cube, root, new Vector3(x, ArenaGeometry.Height + 0.006f, 2.32f), new Vector3(2.2f, 0.012f, 0.06f), Material(Cream));
                var vertices = new[] { new Vector3(x-1.1f,0,-2), new Vector3(x+1.1f,0,-2), new Vector3(x-1.1f,ArenaGeometry.Height,0), new Vector3(x+1.1f,ArenaGeometry.Height,0), new Vector3(x-1.1f,0,0), new Vector3(x+1.1f,0,0) };
                var mesh = new Mesh { name = "Walkable ramp", vertices = vertices, triangles = new[] { 0,2,1, 1,2,3, 0,4,2, 1,3,5, 4,5,2, 2,5,3 } };
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
                var obj = new GameObject("Ramp", typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(root, false);
                obj.GetComponent<MeshFilter>().sharedMesh = mesh;
                obj.GetComponent<MeshRenderer>().sharedMaterial = platform;
                for (int i = 0; i < 4; i++)
                {
                    float z = -1.7f + i * 0.45f;
                    var stripe = Shape("Ramp stripe", PrimitiveType.Cube, root, new Vector3(x, ArenaGeometry.Floor(new Vector3(x,0,z)) + 0.01f, z), new Vector3(1.8f, 0.015f, 0.035f), Material(Cream));
                    stripe.rotation = Quaternion.Euler(-Mathf.Atan(ArenaGeometry.Height / 2) * Mathf.Rad2Deg, 0, 0);
                }
            }
        }

        public void ShowFlight(bool loose, Vector3 position, Color owner, float clock)
        {
            flightShadow.gameObject.SetActive(loose);
            if (!loose) return;
            Potato.gameObject.SetActive(true);
            Potato.position = position;
            Potato.rotation = Quaternion.Euler(clock * 230, clock * 160, 0);
            flightShadow.position = new Vector3(position.x, ArenaGeometry.Floor(position) + 0.035f, position.z);
            flightShadow.GetComponent<MeshRenderer>().sharedMaterial = Material(owner);
        }

        public void Preview(bool visible, Vector3 start, Vector3 velocity)
        {
            Aim.gameObject.SetActive(visible);
            bool landed = false;
            for (int i = 0; i < arcDots.Length; i++)
            {
                Vector3 point = ArenaGeometry.Arc(start, velocity, (i + 1) * 0.055f / ArenaGeometry.ThrowSpeedScale);
                float floor = ArenaGeometry.Floor(point) + 0.23f;
                bool end = point.y <= floor || ArenaGeometry.Flat(point).magnitude >= HotPotatoGame.ArenaRadius;
                arcDots[i].gameObject.SetActive(visible && !landed && !end);
                arcDots[i].position = point;
                if (!landed)
                {
                    Vector3 landing = ArenaGeometry.Clamp(point);
                    Aim.position = new Vector3(landing.x, ArenaGeometry.Floor(landing) + 0.04f, landing.z);
                }
                landed |= end;
            }
        }

        Mesh CreateArenaCylinder()
        {
            const int count = 128;
            var vertices = new Vector3[count * 4 + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[count * 12];
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector3 radial = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                vertices[i * 2] = vertices[count * 2 + i] = radial * 0.5f + Vector3.up;
                vertices[i * 2 + 1] = vertices[count * 3 + i] = radial * 0.5f + Vector3.down;
                normals[i * 2] = normals[i * 2 + 1] = radial;
                normals[count * 2 + i] = Vector3.up;
                normals[count * 3 + i] = Vector3.down;
                int next = (i + 1) % count;
                int t = i * 12;
                triangles[t] = i * 2; triangles[t + 1] = i * 2 + 1; triangles[t + 2] = next * 2;
                triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2 + 1; triangles[t + 5] = next * 2;
                triangles[t + 6] = count * 4; triangles[t + 7] = count * 2 + i; triangles[t + 8] = count * 2 + next;
                triangles[t + 9] = count * 4 + 1; triangles[t + 10] = count * 3 + next; triangles[t + 11] = count * 3 + i;
            }
            vertices[count * 4] = normals[count * 4] = Vector3.up;
            vertices[count * 4 + 1] = normals[count * 4 + 1] = Vector3.down;
            var mesh = new Mesh { name = "Smooth arena cylinder", vertices = vertices, normals = normals, triangles = triangles };
            mesh.RecalculateBounds();
            meshes.Add(mesh);
            return mesh;
        }

        public void Pass(Vector3 from, Vector3 to, bool fromFlight = false)
        {
            passFrom = fromFlight ? from : from + Vector3.up * 1.7f;
            passFlight = 1;
            Burst(to + Vector3.up * 0.6f, 9, 2, false);
            shake = 0.045f;
            Play(passClip);
        }

        public void Explode(Vector3 position, bool winner)
        {
            Burst(position + Vector3.up * 0.8f, winner ? 72 : 40, winner ? 7 : 5, winner);
            shake = winner ? 0.15f : 0.28f;
            Play(winner ? winClip : popClip);
        }

        public void TickSound() => Play(tickClip);
        void Play(AudioClip clip) { if (!Muted) audio.PlayOneShot(clip); }

        public void Burst(Vector3 position, int count, float speed, bool multicolor)
        {
            for (int i = 0; i < count; i++)
            {
                var p = particles[particleCursor++ % particles.Length];
                p.Transform.gameObject.SetActive(true);
                p.Transform.position = position;
                p.Velocity = Random.insideUnitSphere * speed + Vector3.up * speed * 0.8f;
                p.Duration = p.Life = Random.Range(0.35f, 0.85f) * (multicolor ? 2 : 1);
                p.Size = Random.Range(0.08f, 0.19f);
                p.Renderer.sharedMaterial = Material(multicolor ? Palette[i % Palette.Length] : (i % 2 == 0 ? Coral : Cream));
            }
        }

        public void Update(float dt, float clock, PotatoActor holder, float urgency)
        {
            FrameCamera(dt);
            shake = Mathf.MoveTowards(shake, 0, dt * 0.8f);
            Camera.transform.position = new Vector3(Mathf.Sin(clock * 81) * shake, 17 + Mathf.Cos(clock * 67) * shake, -12);
            Potato.gameObject.SetActive(holder != null);
            if (holder != null)
            {
                var target = holder.Position + Vector3.up * (1.72f + holder.ChargeTime * 0.35f + Mathf.Sin(clock * 7) * 0.06f);
                passFlight = Mathf.Max(0, passFlight - dt * 8);
                potatoPosition = Vector3.Lerp(target, passFrom, passFlight) + Vector3.up * Mathf.Sin(passFlight * Mathf.PI) * 0.65f;
                Potato.position = potatoPosition;
                Potato.rotation = Quaternion.Euler(0, Mathf.Sin(clock * 2) * 12, Mathf.Sin(clock * 7) * 8);
            }
            float pulse = 1 + Mathf.Sin(clock * Mathf.Lerp(7, 23, urgency)) * Mathf.Lerp(0.03f, 0.14f, urgency);
            Potato.localScale = Vector3.one * pulse;
            potatoMaterial.color = Color.Lerp(new Color(0.68f, 0.35f, 0.14f), Coral, urgency * (0.5f + 0.5f * Mathf.Sin(clock * 20)));
            foreach (var p in particles)
            {
                if (p.Life <= 0) continue;
                p.Life -= dt;
                if (p.Life <= 0) { p.Transform.gameObject.SetActive(false); continue; }
                p.Velocity += Vector3.down * (12 * dt);
                p.Transform.position += p.Velocity * dt;
                p.Transform.Rotate(new Vector3(120, 170, 90) * dt);
                p.Transform.localScale = Vector3.one * (p.Size * Mathf.Min(1, p.Life * 5));
            }
        }

        static AudioClip Tone(string name, float start, float end, float duration, bool noise)
        {
            const int rate = 22050;
            var samples = new float[(int)(duration * rate)];
            float phase = 0;
            var random = new System.Random(17);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / samples.Length;
                phase += Mathf.Lerp(start, end, t) * Mathf.PI * 2 / rate;
                float wave = Mathf.Sin(phase) * 0.6f + Mathf.Sin(phase * 2) * 0.15f;
                if (noise) wave = wave * 0.5f + ((float)random.NextDouble() * 2 - 1) * 0.4f;
                samples[i] = wave * Mathf.Min(1, t * 40) * Mathf.Pow(1 - t, 2);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void Dispose()
        {
            foreach (var material in materials.Values) Object.Destroy(material);
            foreach (var mesh in meshes) Object.Destroy(mesh);
            Object.Destroy(passClip); Object.Destroy(popClip); Object.Destroy(tickClip); Object.Destroy(winClip);
        }
    }
}
