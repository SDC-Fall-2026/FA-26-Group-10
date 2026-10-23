using System;
using UnityEngine;

namespace HotPotato
{
    public sealed partial class HotPotatoGame
    {
        // Set on an inactive GameObject before Awake. The normal scene stays interactive.
        public bool TrainingMode { get; set; }
        public float ActionCooldown => dashCooldown;
        public float LooseBombAge => bombAge;
        bool[] agentControlled;
        PotatoBotCommand[] commands;
        float[] agentChargeTime;

        void ResetCommands(int count)
        {
            agentControlled = new bool[count];
            commands = new PotatoBotCommand[count];
            agentChargeTime = new float[count];
        }

        void ClearCommands()
        {
            Array.Clear(commands, 0, commands.Length);
            Array.Clear(agentChargeTime, 0, agentChargeTime.Length);
        }

        public void SetAgentControl(int index, bool enabled)
        {
            agentControlled[index] = enabled;
            commands[index] = default;
            agentChargeTime[index] = 0;
            Actors[index].BotChargeGoal = Actors[index].ChargeTime = 0;
        }

        public void SubmitBotCommand(int index, PotatoBotCommand command)
        {
            if (!agentControlled[index]) return;
            command.Move = Vector2.ClampMagnitude(command.Move, 1);
            command.Aim = Vector2.ClampMagnitude(command.Aim, 1);
            commands[index] = command;
        }

        Vector3 AgentMovement(PotatoActor actor, float dt)
        {
            int index = actor.Index;
            var command = commands[index];
            Vector3 aim = new Vector3(command.Aim.x, 0, command.Aim.y);
            if (aim.sqrMagnitude < 0.01f) aim = actor.Facing;
            if (command.Jump) actor.JumpBuffer = 0.12f;
            if (command.Lunge && agentChargeTime[index] <= 0) Dash(actor, aim);
            commands[index].Jump = commands[index].Lunge = false; // Consume impulses once.
            if (Rules.Holder == index && !Rules.Loose)
            {
                if (command.HoldThrow)
                {
                    agentChargeTime[index] = Mathf.Min(PotatoInput.FullChargeTime, agentChargeTime[index] + dt);
                    actor.ChargeTime = Mathf.Clamp01((agentChargeTime[index] - PotatoInput.ThrowThreshold) /
                        (PotatoInput.FullChargeTime - PotatoInput.ThrowThreshold));
                }
                else if (agentChargeTime[index] > 0)
                {
                    if (agentChargeTime[index] >= PotatoInput.ThrowThreshold) TryThrow(actor, aim, actor.ChargeTime);
                    else Dash(actor, aim);
                    actor.ChargeTime = agentChargeTime[index] = 0;
                }
            }
            else actor.ChargeTime = agentChargeTime[index] = 0;
            return new Vector3(command.Move.x, 0, command.Move.y);
        }

        public void ResetTrainingDuel(int seed, float spawnRadiusScale = 1, int initialHolder = -1)
        {
            if (!TrainingMode) throw new InvalidOperationException("Training reset is only valid in a training environment.");
            if (initialHolder < -1 || initialHolder > 1) throw new ArgumentOutOfRangeException(nameof(initialHolder));
            UnityEngine.Random.InitState(seed);
            if (Actors.Length != 2)
            {
                CurrentStage = Stage.Lobby;
                SetPlayerCount(2);
            }
            Rules.Reset(2);
            ClearCommands();
            controls.Reset();
            Paused = false;
            Passes = 0;
            BombPosition = BombVelocity = Vector3.zero;
            BombResting = false;
            bombAge = clock = StageTime = AnnouncementTime = 0;
            Announcement = "";
            queuedJump = false;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2);
            for (int i = 0; i < Actors.Length; i++)
            {
                var actor = Actors[i];
                float a = angle + i * Mathf.PI + UnityEngine.Random.Range(-0.3f, 0.3f);
                actor.Position = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * UnityEngine.Random.Range(2.5f, 4.5f) * Mathf.Clamp(spawnRadiusScale, 0.25f, 1);
                actor.Position.y = ArenaGeometry.Floor(actor.Position);
                actor.Facing = -ArenaGeometry.Flat(actor.Position).normalized;
                actor.Velocity = actor.DashDirection = actor.Wander = actor.ThrowDirection = Vector3.zero;
                actor.Alive = actor.Grounded = true;
                actor.TagArmed = false;
                actor.DashTime = actor.Cooldown = actor.BrainTime = actor.Bounce = actor.VerticalSpeed = 0;
                actor.CoyoteTime = actor.JumpBuffer = actor.EscapeTime = actor.Recovery = 0;
                actor.ChargeTime = actor.BotChargeGoal = 0;
                actor.Target = -1;
                actor.Root.gameObject.SetActive(true);
                actor.Root.position = actor.Position;
            }
            int randomHolder = UnityEngine.Random.Range(0, 2);
            BeginRound(initialHolder >= 0 ? initialHolder : randomHolder);
        }

        public void StepTraining(float dt)
        {
            if (!TrainingMode) throw new InvalidOperationException("Only the training driver may call StepTraining.");
            if (dt <= 0 || dt > 0.1f) throw new ArgumentOutOfRangeException(nameof(dt));
            int steps = Mathf.CeilToInt(dt / (1f / 90f));
            for (int i = 0; i < steps && CurrentStage == Stage.Playing; i++) Simulate(dt / steps, false);
        }
    }
}
