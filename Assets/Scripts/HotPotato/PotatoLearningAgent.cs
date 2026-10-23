using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace HotPotato
{
    public sealed class PotatoLearningAgent : Agent
    {
        public const string Behavior = "HotPotatoV1";
        public const int ObservationCount = 181;
        public const float DecisionInterval = 0.1f;
        public HotPotatoGame Game { get; private set; }
        public int ActorIndex { get; private set; }
        public int DecisionsReceived { get; private set; }
        PotatoActor[] boundCast;
        float decisionClock;
        bool subscribed;

        // Configure while inactive so the first policy initialization has the correct schema.
        public static PotatoLearningAgent Create(HotPotatoGame game, int index, ModelAsset model = null, bool training = false)
        {
            var obj = new GameObject("Learning policy " + index);
            obj.SetActive(false);
            obj.transform.SetParent(game.transform, false);
            var behavior = obj.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = Behavior;
            behavior.BrainParameters.VectorObservationSize = ObservationCount;
            behavior.BrainParameters.ActionSpec = new ActionSpec(4, new[] { 3, 2 });
            behavior.Model = model;
            behavior.BehaviorType = training ? BehaviorType.Default : BehaviorType.InferenceOnly;
            behavior.DeterministicInference = true;
            var agent = obj.AddComponent<PotatoLearningAgent>();
            agent.Game = game;
            agent.ActorIndex = index;
            agent.MaxStep = 0; // The arena, not an arbitrary agent timer, ends the episode.
            obj.SetActive(true);
            return agent;
        }

        public override void Initialize()
        {
            boundCast = Game.Actors;
            Game.SetAgentControl(ActorIndex, true);
            Academy.Instance.AgentPreStep += BeforeAcademyStep;
            subscribed = true;
            decisionClock = DecisionInterval;
        }

        protected override void OnDisable()
        {
            if (subscribed && Academy.IsInitialized) Academy.Instance.AgentPreStep -= BeforeAcademyStep;
            subscribed = false;
            if (Game != null && Game.Actors == boundCast && ActorIndex < Game.Actors.Length)
                Game.SetAgentControl(ActorIndex, false);
            base.OnDisable();
        }

        public override void OnEpisodeBegin()
        {
            decisionClock = DecisionInterval;
        }

        void BeforeAcademyStep(int step)
        {
            if (Game == null || Game.Actors != boundCast || Game.Paused || Game.CurrentStage != HotPotatoGame.Stage.Playing || !Game.Actors[ActorIndex].Alive)
                return;
            decisionClock += Time.fixedDeltaTime;
            if (decisionClock + 0.00001f < DecisionInterval) return;
            decisionClock = 0;
            RequestDecision();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            // ML-Agents requests a final observation during OnDisable, including
            // when a rematch has already replaced this policy's actor array.
            if (Game == null || Game.Actors != boundCast)
            {
                for (int i = 0; i < ObservationCount; i++) sensor.AddObservation(0f);
                return;
            }
            var self = Game.Actors[ActorIndex];
            var rules = Game.Rules;
            sensor.AddObservation(self.Position / 6f);
            sensor.AddObservation(self.Velocity / 15f);
            sensor.AddObservation(new Vector2(self.Facing.x, self.Facing.z));
            sensor.AddObservation(self.VerticalSpeed / 10f);
            sensor.AddObservation(self.Grounded);
            sensor.AddObservation(self.Cooldown / Game.ActionCooldown);
            sensor.AddObservation(self.ChargeTime);
            sensor.AddObservation(self.DashTime / 0.15f);
            sensor.AddObservation(self.Recovery / 0.16f);
            sensor.AddObservation(self.EscapeTime / 0.35f);
            sensor.AddObservation(rules.Immunity(ActorIndex) / MatchRules.ReturnProtection);
            sensor.AddObservation(rules.Holder == ActorIndex);
            sensor.AddObservation(rules.Holder == ActorIndex && !rules.Loose);
            sensor.AddObservation(rules.Fuse / 12f);
            sensor.AddObservation(rules.Protection / MatchRules.PassProtection);
            sensor.AddObservation(rules.Loose);
            sensor.AddObservation(rules.Remaining / 12f);
            Vector3 bomb = rules.Loose ? Game.BombPosition :
                rules.Holder >= 0 ? HotPotatoGame.ThrowOrigin(Game.Actors[rules.Holder]) : self.Position;
            sensor.AddObservation((bomb - self.Position) / 12f);
            sensor.AddObservation(rules.Loose ? Game.BombVelocity / 15f : Vector3.zero);
            sensor.AddObservation(Game.BombResting);
            sensor.AddObservation(Mathf.Clamp01(Game.LooseBombAge / 0.45f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector3 probe = self.Position + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                sensor.AddObservation((ArenaGeometry.Floor(probe) - self.Position.y) / 3f);
            }
            // Fixed slots keep the exported interface valid for 2-12 players.
            // Missing/dead slots are zero; the first value is the alive mask.
            int slot = 0;
            for (int i = 0; i < Game.Actors.Length; i++)
            {
                if (i == ActorIndex) continue;
                slot++;
                var other = Game.Actors[i];
                if (!other.Alive) { for (int j = 0; j < 13; j++) sensor.AddObservation(0f); continue; }
                sensor.AddObservation(1f);
                sensor.AddObservation((other.Position - self.Position) / 12f);
                sensor.AddObservation(other.Velocity / 15f);
                sensor.AddObservation(other.VerticalSpeed / 10f);
                sensor.AddObservation(rules.Holder == i);
                sensor.AddObservation(rules.Immunity(i) / MatchRules.ReturnProtection);
                sensor.AddObservation(other.Cooldown / Game.ActionCooldown);
                sensor.AddObservation(other.Grounded);
                sensor.AddObservation(other.ChargeTime);
            }
            for (; slot < 11; slot++) for (int j = 0; j < 13; j++) sensor.AddObservation(0f);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
        {
            if (Game == null || Game.Actors != boundCast)
            {
                mask.SetActionEnabled(0, 1, false);
                mask.SetActionEnabled(0, 2, false);
                mask.SetActionEnabled(1, 1, false);
                return;
            }
            var actor = Game.Actors[ActorIndex];
            mask.SetActionEnabled(0, 1, actor.Cooldown <= 0 && actor.ChargeTime <= 0);
            mask.SetActionEnabled(0, 2, Game.Rules.Holder == ActorIndex && !Game.Rules.Loose);
            mask.SetActionEnabled(1, 1, actor.Grounded || actor.CoyoteTime > 0);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (Game.Actors != boundCast || Game.CurrentStage != HotPotatoGame.Stage.Playing || Game.Paused) return;
            DecisionsReceived++;
            var c = actions.ContinuousActions;
            var d = actions.DiscreteActions;
            Game.SubmitBotCommand(ActorIndex, new PotatoBotCommand
            {
                Move = new Vector2(c[0], c[1]), Aim = new Vector2(c[2], c[3]),
                Lunge = d[0] == 1, HoldThrow = d[0] == 2, Jump = d[1] == 1
            });
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // A zero-action fallback is intentionally visible as "not trained".
            actionsOut.Clear();
        }
    }
}
