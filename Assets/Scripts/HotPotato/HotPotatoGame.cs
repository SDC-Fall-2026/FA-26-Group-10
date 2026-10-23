using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPotato
{
    [DisallowMultipleComponent]
    public sealed partial class HotPotatoGame : MonoBehaviour
    {
        public enum Stage { Lobby, Countdown, Playing, Intermission, Finished }
        [Header("Game feel")]
        [SerializeField, Range(2, 12)] int playerCount = 8;
        [SerializeField, Range(3, 9)] float moveSpeed = 5.2f;
        [SerializeField, Range(10, 60)] float acceleration = 32;
        [SerializeField, Range(8, 22)] float dashSpeed = 15;
        [SerializeField, Range(0.5f, 2)] float dashCooldown = 1.05f;
        public const float ArenaRadius = 5.48f;
        public const float TagDistance = 1.05f;
        public MatchRules Rules { get; } = new MatchRules();
        public Stage CurrentStage { get; private set; }
        public PotatoActor[] Actors { get; private set; }
        public bool Paused { get; private set; }
        public int PlayerCount => playerCount;
        public float StageTime { get; private set; }
        public float DashReady => Actors == null ? 1 : 1 - Mathf.Clamp01(Actors[0].Cooldown / dashCooldown);
        public bool Muted => view != null && view.Muted;
        public string Announcement { get; private set; } = "";
        public float AnnouncementTime { get; private set; }
        public int Passes { get; private set; }
        public PotatoView View => view;
        public Vector3 BombPosition { get; private set; }
        public Vector3 BombVelocity { get; private set; }
        public bool BombResting { get; private set; }
        public float Charge => Rules.Holder == 0 && !Rules.Loose && controls.PrimaryHeld ? controls.Charge : 0;
        public bool AimingThrow => Rules.Holder == 0 && !Rules.Loose && controls.PrimaryHeld;
        PotatoView view;
        PotatoHud hud;
        PotatoInput controls;
        Transform cast;
        float clock;
        float bombAge;
        int lastTick;
        int previousFrameRate;
        bool queuedJump;
        static readonly string[] Names = { "YOU", "MOCHI", "PIP", "BUBBLES", "KIWI", "TANGO", "MINT", "PEACH", "BLUE", "NUGGET", "POPPY", "CLOUD" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapTemplate()
        {
            if (SceneManager.GetActiveScene().name != "SampleScene" || FindAnyObjectByType<HotPotatoGame>() != null) return;
            foreach (var camera in FindObjectsByType<Camera>()) camera.gameObject.SetActive(false);
            new GameObject("Hot Potato Tag").AddComponent<HotPotatoGame>();
        }

        void Awake()
        {
            previousFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = 120;
            controls = new PotatoInput();
            if (!TrainingMode)
            {
                view = new PotatoView(transform);
                hud = new PotatoHud(this, transform);
            }
            SetPlayerCount(playerCount);
        }

        public void SetPlayerCount(int count)
        {
            if (CurrentStage != Stage.Lobby) return;
            playerCount = Mathf.Clamp(count, 2, 12);
            CreateCast();
        }

        void CreateCast()
        {
            if (cast != null) { cast.gameObject.SetActive(false); Destroy(cast.gameObject); }
            cast = new GameObject("Contestants").transform;
            cast.SetParent(transform, false);
            Actors = new PotatoActor[playerCount];
            ResetCommands(playerCount);
            Rules.Reset(playerCount);
            BombPosition = BombVelocity = Vector3.zero;
            BombResting = false;
            queuedJump = false;
            for (int i = 0; i < playerCount; i++)
            {
                float angle = i * Mathf.PI * 2 / playerCount + Mathf.PI;
                var actor = Actors[i] = new PotatoActor(i, Names[i], PotatoView.Palette[i], cast, view);
                actor.Position = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 3.9f;
                actor.Position.y = ArenaGeometry.Floor(actor.Position);
                actor.Facing = -ArenaGeometry.Flat(actor.Position).normalized;
                actor.Root.position = actor.Position;
            }
        }

        public void StartMatch()
        {
            Paused = false;
            CreateCast();
            controls.Reset();
            CurrentStage = Stage.Countdown;
            StageTime = 3.2f;
            Passes = 0;
            lastTick = 4;
            AnnouncementTime = 0;
        }

        public void BackToLobby()
        {
            Paused = false;
            CurrentStage = Stage.Lobby;
            controls.Reset();
            CreateCast();
        }

        public void TogglePause()
        {
            if (CurrentStage == Stage.Lobby || CurrentStage == Stage.Finished) return;
            Paused = !Paused;
            controls.Reset();
            ClearCommands();
            queuedJump = false;
            foreach (var actor in Actors) { actor.ChargeTime = 0; actor.BotChargeGoal = 0; }
        }

        public void RequestJump() { if (!Paused && CurrentStage == Stage.Playing) queuedJump = true; }
        public void ToggleMute() => view.Muted = !view.Muted;
        void OnApplicationFocus(bool focus)
        {
            if (TrainingMode) return;
            if (!focus && !Paused && CurrentStage != Stage.Lobby && CurrentStage != Stage.Finished) TogglePause();
        }
        void OnApplicationPause(bool pause) { if (pause) OnApplicationFocus(false); }

        Vector3 PlayerAim()
        {
            Vector3 direction = controls.HasAim ? ArenaGeometry.Flat(controls.AimPoint - Actors[0].Position)
                : controls.Move.sqrMagnitude > 0.01f ? new Vector3(controls.Move.x, 0, controls.Move.y) : Actors[0].Facing;
            return direction.sqrMagnitude > 0.01f ? direction.normalized : Actors[0].Facing;
        }

        void Update()
        {
            if (TrainingMode) return;
            controls.Read(view, Actors[0].Position, CurrentStage == Stage.Playing && !Paused && Actors[0].Alive);
            if (controls.MutePressed) ToggleMute();
            if (controls.PausePressed) TogglePause();
            if (controls.StartPressed && (CurrentStage == Stage.Lobby || CurrentStage == Stage.Finished)) StartMatch();
            if (controls.RestartPressed && CurrentStage != Stage.Lobby) StartMatch();


            float dt = Paused ? 0 : Mathf.Min(Time.deltaTime, 0.05f);
            clock += dt;
            AnnouncementTime = Mathf.Max(0, AnnouncementTime - dt);
            if (!Paused)
            {
                if (CurrentStage == Stage.Countdown)
                {
                    StageTime -= dt;
                    int tick = Mathf.CeilToInt(StageTime);
                    if (tick != lastTick && tick > 0) { view.TickSound(); lastTick = tick; }
                    if (StageTime <= 0) BeginRound(0);
                }
                else if (CurrentStage == Stage.Playing)
                {
                    if (Actors[0].Alive)
                    {
                        if (controls.JumpPressed || queuedJump) Actors[0].JumpBuffer = 0.12f;
                        Actors[0].ChargeTime = AimingThrow ? controls.Charge : 0;
                        if (controls.ThrowReleased) TryThrow(Actors[0], PlayerAim(), controls.Charge);
                    }
                    queuedJump = false;
                    int steps = Mathf.Max(1, Mathf.CeilToInt(dt / (1f / 90f)));
                    for (int i = 0; i < steps && CurrentStage == Stage.Playing; i++) Simulate(dt / steps, controls.TagPressed && i == 0);

                }
                else if (CurrentStage == Stage.Intermission)
                {
                    StageTime -= dt;
                    if (StageTime <= 0)
                    {
                        int next = Random.Range(0, playerCount);
                        while (!Actors[next].Alive) next = (next + 1) % playerCount;
                        BeginRound(next);
                    }
                }
            }
            PotatoActor responsible = Rules.Holder >= 0 ? Actors[Rules.Holder] :
                CurrentStage == Stage.Lobby || CurrentStage == Stage.Countdown ? Actors[0] : null;
            foreach (var actor in Actors) actor.Animate(dt, clock, actor == responsible, Rules.Immunity(actor.Index) > 0);
            view.Update(dt, clock, Rules.Loose ? null : responsible, Rules.Running ? 1 - Mathf.Clamp01(Rules.Fuse / 5f) : 0);
            view.ShowFlight(Rules.Loose, BombPosition, responsible != null ? responsible.Color : PotatoView.Coral, clock);
            view.Preview(!Paused && CurrentStage == Stage.Playing && AimingThrow && Actors[0].Cooldown <= 0,
                ThrowOrigin(Actors[0]), ArenaGeometry.ThrowVelocity(PlayerAim(), Charge));
            hud.Refresh(controls);
        }

        void BeginRound(int holder)
        {
            if (!Rules.BeginRound(holder)) return;
            CurrentStage = Stage.Playing;
            lastTick = Mathf.CeilToInt(Rules.Fuse);
            BombResting = false;
            controls.Reset();
            ClearCommands();
            foreach (var actor in Actors)
            {
                actor.DashTime = actor.Recovery = actor.ChargeTime = actor.BotChargeGoal = 0;
                actor.TagArmed = false;
                actor.Velocity = Vector3.zero;
                actor.Target = -1;
            }
        }

        void Simulate(float dt, bool tagPressed)
        {
            int eliminated = Rules.Tick(dt);
            if (eliminated >= 0) { Eliminate(eliminated); return; }
            int tick = Mathf.CeilToInt(Rules.Fuse);
            if (tick != lastTick) { if (tick <= 4) view?.TickSound(); lastTick = tick; }
            foreach (var actor in Actors)
            {
                if (!actor.Alive) continue;

                if (actor.Position.y < -10f)
                {
                    Fall(actor.Index);
                    if (CurrentStage != Stage.Playing) return;
                    continue;
                }
                actor.Cooldown = Mathf.Max(0, actor.Cooldown - dt);
                actor.EscapeTime = Mathf.Max(0, actor.EscapeTime - dt);
                actor.Recovery = Mathf.Max(0, actor.Recovery - dt);
                Vector3 desired;
                if (agentControlled[actor.Index])
                    desired = AgentMovement(actor, dt);
                else if (actor.Index == 0 && !TrainingMode)
                {
                    desired = new Vector3(controls.Move.x, 0, controls.Move.y);
                    if (tagPressed) Dash(actor, PlayerAim());
                }
                else desired = BotMovement(actor, dt);
                if (actor.DashTime > 0)
                    actor.Velocity = actor.DashDirection * dashSpeed;
                else
                {
                    float speed = moveSpeed * (Rules.Holder == actor.Index && !Rules.Loose ? 1.06f : 1);
                    if (actor.EscapeTime > 0) speed *= 1.35f;
                    if (actor.Recovery > 0) speed *= 0.65f;
                    if (actor.BotChargeGoal > 0 || agentChargeTime[actor.Index] > 0 || (actor.Index == 0 && AimingThrow)) speed *= 0.65f;
                    actor.Velocity = Vector3.MoveTowards(actor.Velocity, desired * speed, acceleration * (actor.Grounded ? 1 : 0.4f) * dt);
                }
                MoveActor(actor, dt);
                if (actor.Velocity.sqrMagnitude > 0.1f) actor.Facing = actor.Velocity.normalized;
            }
            ResolveContacts();
            if (Rules.Loose) StepBomb(dt);
            foreach (var actor in Actors)
            {
                if (actor.DashTime <= 0) continue;
                actor.DashTime = Mathf.Max(0, actor.DashTime - dt);
                if (actor.DashTime <= 0) { actor.TagArmed = false; actor.Recovery = 0.16f; }
            }
        }

        void MoveActor(PotatoActor actor, float dt)
        {
            actor.CoyoteTime = actor.Grounded ? 0.1f : Mathf.Max(0, actor.CoyoteTime - dt);
            if (actor.JumpBuffer > 0 && actor.CoyoteTime > 0)
            {
                actor.VerticalSpeed = 8f;
                actor.Grounded = false;
                actor.CoyoteTime = 0;
                actor.JumpBuffer = 0;
                actor.Bounce = 0.6f;
            }
            actor.JumpBuffer = Mathf.Max(0, actor.JumpBuffer - dt);
            Vector3 next = ArenaGeometry.Clamp(actor.Position + actor.Velocity * dt, HotPotatoGame.ArenaRadius + 4f);
            float allowedRise = actor.Grounded ? 0.16f : 0.025f;
            if (ArenaGeometry.Floor(next) > actor.Position.y + allowedRise)
            {
                // Slide along a wall; platform sides cannot be walked through.
                Vector3 slide = new Vector3(next.x, actor.Position.y, actor.Position.z);
                if (ArenaGeometry.Floor(slide) <= actor.Position.y + allowedRise) next = slide;
                else
                {
                    slide = new Vector3(actor.Position.x, actor.Position.y, next.z);
                    next = ArenaGeometry.Floor(slide) <= actor.Position.y + allowedRise ? slide : actor.Position;
                }
            }
            float floor = ArenaGeometry.Floor(next);
            if (actor.Grounded && actor.Position.y - floor <= 0.18f)
                next.y = floor;
            else
            {
                actor.Grounded = false;
                next.y = actor.Position.y + actor.VerticalSpeed * dt - 0.5f * ArenaGeometry.Gravity * dt * dt;
                actor.VerticalSpeed -= ArenaGeometry.Gravity * dt;
                if (next.y <= floor && actor.VerticalSpeed <= 0)
                {
                    next.y = floor;
                    actor.VerticalSpeed = 0;
                    actor.Grounded = true;
                    actor.Bounce = 0.6f;
                }
            }
            actor.Position = next;
        }

        Vector3 BotMovement(PotatoActor actor, float dt)
        {
            actor.BrainTime -= dt;
            bool think = actor.BrainTime <= 0;
            if (think)
            {
                actor.BrainTime = Random.Range(0.22f, 0.4f);
                actor.Wander = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                actor.Target = NearestReceiver(actor);
            }
            var owner = Actors[Rules.Holder];
            Vector3 desired;
            if (Rules.Loose && actor == owner)
                desired = Route(actor, BombPosition);
            else if (!Rules.Loose && actor == owner && actor.Target >= 0)
            {
                var target = Actors[actor.Target];
                Vector3 offset = ArenaGeometry.Flat(target.Position - actor.Position);
                desired = Route(actor, target.Position);
                if (actor.BotChargeGoal > 0)
                {
                    actor.ChargeTime += dt;
                    if (actor.ChargeTime >= actor.BotChargeGoal)
                    {
                        TryThrow(actor, actor.ThrowDirection, Mathf.Clamp01((actor.ChargeTime - PotatoInput.ThrowThreshold) / (PotatoInput.FullChargeTime - PotatoInput.ThrowThreshold)));
                        actor.BotChargeGoal = actor.ChargeTime = 0;
                    }
                }
                else if (think && actor.Cooldown <= 0)
                {
                    if (offset.magnitude < 2.5f && Mathf.Abs(target.Position.y - actor.Position.y) < 0.7f)
                        Dash(actor, offset);
                    else if (offset.magnitude > 3 && offset.magnitude < 7 && Random.value < 0.55f)
                    {
                        actor.BotChargeGoal = Mathf.Lerp(0.3f, 0.85f, Mathf.Clamp01((offset.magnitude - 3) / 4));
                        actor.ChargeTime = 0;
                        // Commit to a prediction; no last-instant perfect tracking or homing.
                        actor.ThrowDirection = ArenaGeometry.Flat(target.Position + target.Velocity * 0.3f - actor.Position).normalized;
                    }
                }
            }
            else
            {
                actor.BotChargeGoal = actor.ChargeTime = 0;
                Vector3 away = ArenaGeometry.Flat(actor.Position - (Rules.Loose ? BombPosition : owner.Position));
                float danger = 1 - Mathf.Clamp01((away.magnitude - 1.2f) / 4);
                desired = away.normalized * danger * 1.5f + Vector3.Cross(away.normalized, Vector3.up) * (actor.Index % 2 == 0 ? 0.45f : -0.45f) + actor.Wander * 0.5f;
                if (ArenaGeometry.Flat(actor.Position).magnitude > 4.3f) desired -= ArenaGeometry.Flat(actor.Position).normalized * 1.6f;
                if (think && actor.Grounded && away.magnitude < 2.5f && Random.value < 0.35f) actor.JumpBuffer = 0.12f;
            }
            foreach (var other in Actors)
            {
                if (!other.Alive || other == actor || Mathf.Abs(other.Position.y - actor.Position.y) > 0.8f) continue;
                Vector3 delta = ArenaGeometry.Flat(actor.Position - other.Position);
                if (delta.magnitude > 0.01f && delta.magnitude < 1.1f) desired += delta.normalized * (1.1f - delta.magnitude);
            }
            return Vector3.ClampMagnitude(desired, 1);
        }

        Vector3 Route(PotatoActor actor, Vector3 destination)
        {
            if (ArenaGeometry.Floor(destination) > actor.Position.y + 0.3f)
            {
                float x = destination.x < 0 ? -3 : 3;
                if (actor.Position.z > -0.2f && Mathf.Abs(actor.Position.x - x) > 1.1f)
                    destination = new Vector3(Mathf.Sign(x) * 4.45f, 0, -2.2f);
                else if (Mathf.Abs(actor.Position.x - x) > 0.8f || actor.Position.z < -2)
                    destination = new Vector3(x, 0, -1.8f);
            }
            return ArenaGeometry.Flat(destination - actor.Position).normalized;
        }

        int NearestReceiver(PotatoActor actor)
        {
            int best = -1;
            float distance = float.MaxValue;
            foreach (var other in Actors)
            {
                if (other == actor || !other.Alive || Rules.Immunity(other.Index) > 0) continue;
                float score = (other.Position - actor.Position).sqrMagnitude;
                if (score < distance) { distance = score; best = other.Index; }
            }
            return best;
        }

        void Dash(PotatoActor actor, Vector3 direction)
        {
            if (!actor.Alive || actor.Cooldown > 0 || actor.BotChargeGoal > 0) return;
            direction = ArenaGeometry.Flat(direction);
            if (direction.sqrMagnitude < 0.01f) direction = actor.Facing;
            actor.DashDirection = direction.normalized;
            actor.DashTime = 0.15f;
            actor.Cooldown = dashCooldown;
            actor.TagArmed = Rules.Holder == actor.Index && !Rules.Loose;
            actor.Bounce = 0.7f;
            view?.Burst(actor.Position + Vector3.up * 0.15f, 4, 1.3f, false);
        }

        public static Vector3 ThrowOrigin(PotatoActor actor) => actor.Position + Vector3.up * (1.72f + actor.ChargeTime * 0.35f);

        bool TryThrow(PotatoActor actor, Vector3 direction, float charge)
        {
            if (!actor.Alive || actor.Cooldown > 0 || actor.DashTime > 0 || !Rules.Throw(actor.Index)) return false;
            BombPosition = ThrowOrigin(actor);
            BombVelocity = ArenaGeometry.ThrowVelocity(direction, Mathf.Clamp01(charge));
            BombResting = false;
            bombAge = 0;
            actor.Cooldown = dashCooldown;
            actor.TagArmed = false;
            actor.ChargeTime = 0;
            view?.TickSound();
            return true;
        }

        void StepBomb(float dt)
        {
            bombAge += dt;
            Vector3 old = BombPosition;
            if (!BombResting)
            {
                Vector3 next = ArenaGeometry.Arc(old, BombVelocity, dt);
                Vector3 velocity = BombVelocity + Vector3.down * ArenaGeometry.BombGravity * dt;
                float floor = ArenaGeometry.Floor(next) + 0.23f;
                if (next.y < floor)
                {
                    if (old.y < floor - 0.1f && ArenaGeometry.Floor(old) < floor - 0.3f)
                    {
                        next.x = old.x; next.z = old.z;
                        velocity.x *= -0.55f; velocity.z *= -0.55f;
                        floor = ArenaGeometry.Floor(next) + 0.23f;
                    }
                    if (next.y < floor)
                    {
                        next.y = floor;
                        velocity.y = Mathf.Abs(velocity.y) > 2 ? Mathf.Abs(velocity.y) * 0.3f : 0;
                        velocity.x *= 0.6f; velocity.z *= 0.6f;
                        if (velocity.sqrMagnitude < 0.4f) { velocity = Vector3.zero; BombResting = true; }
                    }
                }
                if (ArenaGeometry.Flat(next).magnitude > ArenaRadius)
                {
                    Vector3 normal = ArenaGeometry.Flat(next).normalized;
                    next = ArenaGeometry.Clamp(next);
                    velocity = Vector3.Reflect(velocity, normal) * 0.6f;
                }
                BombPosition = next;
                BombVelocity = velocity;
            }
            // Pick the first intersected capsule along this step, not a target chosen by aim assistance.
            int hit = -1;
            float first = float.MaxValue;
            Vector3 segment = BombPosition - old;
            foreach (var actor in Actors)
            {
                if (!actor.Alive || (actor.Index == Rules.Holder && bombAge < 0.45f)) continue;
                if (actor.Index != Rules.Holder && !Rules.CanReceive(actor.Index)) continue;
                Vector3 center = actor.Position + Vector3.up * 0.7f;
                float t = segment.sqrMagnitude > 0.00001f ? Mathf.Clamp01(Vector3.Dot(center - old, segment) / segment.sqrMagnitude) : 0;
                Vector3 closest = old + segment * t;
                float vertical = Mathf.Clamp(closest.y, actor.Position.y + 0.35f, actor.Position.y + 1.1f);
                if ((closest - new Vector3(center.x, vertical, center.z)).sqrMagnitude > 0.54f * 0.54f || t >= first) continue;
                hit = actor.Index; first = t;
            }
            if (hit >= 0)
            {
                int from = Rules.Holder;
                if (Rules.Catch(hit))
                {
                    if (from != hit) OnPass(from, hit, true);
                    else { Actors[hit].TagArmed = false; Actors[hit].DashTime = 0; }
                }
            }
        }

        void OnPass(int from, int to, bool fromFlight = false)
        {
            var sender = Actors[from]; var receiver = Actors[to];
            sender.DashTime = 0;
            sender.TagArmed = false;
            sender.EscapeTime = 0.35f;
            receiver.DashTime = receiver.ChargeTime = receiver.BotChargeGoal = 0;
            receiver.TagArmed = false;
            receiver.Cooldown = Mathf.Max(receiver.Cooldown, 0.25f);
            agentChargeTime[from] = agentChargeTime[to] = 0;
            receiver.Bounce = 1;
            view?.Pass(fromFlight ? BombPosition : sender.Position, receiver.Position, fromFlight);
            if (from == 0) Passes++;
        }

        void ResolveContacts()
        {
            for (int i = 0; i < Actors.Length; i++)
                for (int j = i + 1; j < Actors.Length; j++)
                {
                    var a = Actors[i]; var b = Actors[j];
                    if (!a.Alive || !b.Alive || Mathf.Abs(a.Position.y - b.Position.y) > 0.75f) continue;
                    Vector3 delta = ArenaGeometry.Flat(b.Position - a.Position);
                    float distance = delta.magnitude;
                    if (!Rules.Loose && distance < TagDistance && (Rules.Holder == i || Rules.Holder == j))
                    {
                        int from = Rules.Holder; int to = from == i ? j : i;
                        var carrier = Actors[from];
                        Vector3 toward = ArenaGeometry.Flat(Actors[to].Position - carrier.Position).normalized;
                        if (carrier.TagArmed && carrier.DashTime > 0 && Vector3.Dot(carrier.DashDirection, toward) > 0.15f && Rules.TryPass(from, to))
                            OnPass(from, to);
                    }
                    if (distance >= 0.78f) continue;
                    Vector3 push = (distance > 0.001f ? delta / distance : Vector3.right) * ((0.78f - distance) * 0.5f);
                    Separate(a, -push); Separate(b, push);
                }
        }

        void Separate(PotatoActor actor, Vector3 push)
        {
            Vector3 point = ArenaGeometry.Clamp(actor.Position + push, HotPotatoGame.ArenaRadius + 4f);
            if (ArenaGeometry.Floor(point) <= actor.Position.y + 0.1f) actor.Position = point;
        }

        void Eliminate(int index)
        {
            var actor = Actors[index];
            actor.Alive = false;
            actor.Root.gameObject.SetActive(false);
            view?.Explode(actor.Position, false);
            foreach (var survivor in Actors) { survivor.Velocity = Vector3.zero; survivor.TagArmed = false; survivor.ChargeTime = survivor.BotChargeGoal = 0; }
            if (Rules.Finished)
            {
                CurrentStage = Stage.Finished;
                foreach (var survivor in Actors)
                    if (survivor.Alive) { Say(survivor.Index == 0 ? "YOU WIN" : survivor.Name + " WINS", 20); view?.Explode(survivor.Position, true); }
            }
            else
            {
                CurrentStage = Stage.Intermission;
                StageTime = 1.3f;
                Say(actor.Name + " OUT", 1.3f);
            }
        }

        void Fall(int index)
        {
            var actor = Actors[index];
            bool wasHolder = Rules.Holder == index;
            if (!Rules.Remove(index)) return;

            if (wasHolder || Rules.Finished)
            {
                Eliminate(index); // ends the round: intermission or win
                return;
            }

            // Non-holder: just remove them and keep playing
            actor.Alive = false;
            actor.Root.gameObject.SetActive(false);
            view?.Explode(actor.Position, false);
            Say(actor.Name + " OUT", 1.3f);
            foreach (var other in Actors)
                if (other.Target == index) other.Target = -1;
        }

        void Say(string message, float duration) { Announcement = message; AnnouncementTime = duration; }
        void OnDestroy()
        {
            view?.Dispose();
            hud?.Dispose();
            Application.targetFrameRate = previousFrameRate;
        }
    }
}
