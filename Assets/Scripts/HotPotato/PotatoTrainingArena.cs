using System;
using UnityEngine;
using Unity.InferenceEngine;
using Unity.MLAgents;

namespace HotPotato
{
    // Academy applies actions first; this driver then advances the actual game simulation.
    [DefaultExecutionOrder(100)]
    public sealed class PotatoTrainingArena : MonoBehaviour
    {
        public int seed = 12345;
        public ModelAsset evaluationModel;
        public int evaluationEpisodes;
        public HotPotatoGame Game { get; private set; }
        public PotatoLearningAgent Learner { get; private set; }
        int episodes, wins;
        public int PracticeEpisodes { get; private set; }
        public int PracticeSuccesses { get; private set; }
        float elapsed;
        float previousFixedDelta;
        float previousTimeScale;
        bool resetPending;
        bool scriptedEvaluation;
        float shapingClock, previousPotential;
        bool shaping;
        int practice;

        void Start()
        {
            previousFixedDelta = Time.fixedDeltaTime;
            previousTimeScale = Time.timeScale;
            Time.fixedDeltaTime = 0.02f;
            string[] args = Environment.GetCommandLineArgs();
            int seedArg = Array.IndexOf(args, "--potato-seed");
            if (seedArg >= 0 && seedArg + 1 < args.Length) seed = int.Parse(args[seedArg + 1]);
            int evaluateArg = Array.IndexOf(args, "--potato-evaluate");
            if (evaluateArg >= 0 && evaluateArg + 1 < args.Length)
            {
                evaluationEpisodes = int.Parse(args[evaluateArg + 1]);
                if (evaluationEpisodes < 1) throw new ArgumentOutOfRangeException(nameof(evaluationEpisodes));
                scriptedEvaluation = Array.IndexOf(args, "--potato-scripted") >= 0;
                if (!scriptedEvaluation)
                {
                    evaluationModel = Resources.Load<ModelAsset>("HotPotato/Models/Experimental");
                    if (evaluationModel == null) throw new InvalidOperationException("Import Experimental.onnx and rebuild before evaluating.");
                }
            }
            if (evaluationEpisodes > 0) Time.timeScale = 20;
            var obj = new GameObject("Training duel");
            obj.SetActive(false);
            obj.transform.SetParent(transform, false);
            Game = obj.AddComponent<HotPotatoGame>();
            Game.TrainingMode = true;
            obj.SetActive(true);
            Game.ResetTrainingDuel(seed);
            if (!scriptedEvaluation) Learner = PotatoLearningAgent.Create(Game, 0, evaluationModel, evaluationModel == null);
            if (Learner != null) Academy.Instance.OnEnvironmentReset += EnvironmentReset;
            ResetDuel();
            Debug.Log("HOT_POTATO_TRAINING_READY: " + PotatoLearningAgent.Behavior);
        }

        void FixedUpdate()
        {
            if (Game == null) return;
            if (resetPending)
            {
                ResetDuel();
                elapsed = 0;
                resetPending = false;
                // No simulation before the next Academy decision.
                return;
            }
            Game.StepTraining(Time.fixedDeltaTime);
            elapsed += Time.fixedDeltaTime;
            shapingClock += Time.fixedDeltaTime;
            if (shaping && (shapingClock + 0.00001f >= PotatoLearningAgent.DecisionInterval || Game.Rules.Finished))
            {
                float nextPotential = Game.Rules.Finished ? 0 : Potential();
                // Potential difference at the policy decision cadence; terminal potential is zero.
                // The discount matches the curriculum config. This avoids a repeatable pass bonus.
                Learner.AddReward(0.995f * nextPotential - previousPotential);
                previousPotential = nextPotential;
                shapingClock = 0;
            }
            // Bounded shaping: at most -0.12 per duel; loose-bomb responsibility counts.
            if (practice == 0 && Game.Rules.Holder == 0) Learner?.AddReward(-0.01f * Time.fixedDeltaTime);
            bool transferred = Game.Passes > 0 && Game.Actors[0].Alive;
            bool matchFinished = Game.CurrentStage == HotPotatoGame.Stage.Finished;
            if (matchFinished || (practice > 0 && transferred))
            {
                bool won = matchFinished && Game.Actors[0].Alive;
                if (won) wins++;
                // In practice, cumulative reward is exactly transfer success (0 or 1).
                // No shaping or match-win bonus may contaminate lesson promotion.
                Learner?.AddReward(practice > 0 ? (transferred ? 1 : 0) : (won ? 1 : -1));
                if (practice > 0)
                {
                    PracticeEpisodes++;
                    if (transferred) PracticeSuccesses++;
                }
                if (Academy.IsInitialized)
                {
                    if (practice > 0)
                        Academy.Instance.StatsRecorder.Add("Practice/TransferSuccess", transferred ? 1 : 0);
                    else Academy.Instance.StatsRecorder.Add("Duel/WinRate", won ? 1 : 0);
                    Academy.Instance.StatsRecorder.Add("Duel/Passes", Game.Passes);
                    Academy.Instance.StatsRecorder.Add("Duel/PracticeLevel", practice);
                }
                episodes++;
                Learner?.EndEpisode();
                resetPending = true;
                if (evaluationEpisodes > 0 && episodes >= evaluationEpisodes)
                {
                    string result = $"episodes={episodes}, wins={wins}, winRate={(float)wins / episodes:0.000}, seed={seed}, scripted={scriptedEvaluation}";
                    Debug.Log("HOT_POTATO_EVALUATION: " + result);
                    System.IO.File.WriteAllText("HotPotato-evaluation.txt", result);
                    enabled = false;
                    Application.Quit(0);
                }
            }
            else if (elapsed > 15)
                throw new InvalidOperationException("A training duel failed to finish within its 12-second fuse.");
        }

        void ResetDuel()
        {
            bool training = evaluationEpisodes == 0 && evaluationModel == null && Academy.IsInitialized;
            int lesson = training ? Mathf.RoundToInt(Academy.Instance.EnvironmentParameters.GetWithDefault("practice", 0)) : 0;
            ConfigureDuel(lesson, training && Academy.Instance.EnvironmentParameters.GetWithDefault("reward_shaping", 0) > 0.5f);
        }

        void ConfigureDuel(int lesson, bool enableShaping)
        {
            practice = Mathf.Clamp(lesson, 0, 2);
            shaping = enableShaping && practice == 0;
            Game.ResetTrainingDuel(seed + episodes, practice >= 2 ? 0.25f : 1, practice > 0 ? 0 : -1);
            Game.SetAgentControl(1, practice > 0);
            previousPotential = Potential();
            shapingClock = elapsed = 0;
            resetPending = false;
        }

        float Potential()
        {
            if (Game.Rules.Holder != 0) return 0;
            Vector3 target = Game.Rules.Loose ? Game.BombPosition : Game.Actors[1].Position;
            return -0.5f * Mathf.Clamp01(Vector3.Distance(Game.Actors[0].Position, target) / (2 * HotPotatoGame.ArenaRadius));
        }

        void EnvironmentReset() => resetPending = true;

        void OnDestroy()
        {
            if (Academy.IsInitialized) Academy.Instance.OnEnvironmentReset -= EnvironmentReset;
            if (previousFixedDelta > 0) Time.fixedDeltaTime = previousFixedDelta;
            if (previousTimeScale > 0) Time.timeScale = previousTimeScale;
        }
    }
}
