using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotPotato.Editor
{
    public static class PotatoTrainingTools
    {
        const string ScenePath = "Assets/Scenes/HotPotatoTraining.unity";

        [MenuItem("Tools/Hot Potato/Open Training Scene")]
        public static void OpenTrainingScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath);
            else CreateScene();
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Hot Potato training").AddComponent<PotatoTrainingArena>();
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // Run in the isolated copy so building never replaces a teammate's open scene.
        public static void Build()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!File.Exists(ScenePath)) CreateScene();
                string output = Argument("-potatoOutput") ?? "Builds/Training/HotPotatoTraining.exe";
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath }, locationPathName = output,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
                });
                if (result.summary.result != BuildResult.Succeeded) throw new Exception("Training build failed: " + result.summary.result);
                Debug.Log("HOT_POTATO_TRAINING_BUILD_OK: " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        public static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        public static void ValidateTrainingContract()
        {
            var root = new GameObject("Training contract");
            root.SetActive(false);
            var game = root.AddComponent<HotPotatoGame>();
            game.TrainingMode = true;
            root.SetActive(true);
            try
            {
                game.ResetTrainingDuel(731);
                Vector3 spawn = game.Actors[0].Position;
                int holder = game.Rules.Holder;
                game.SetAgentControl(0, true);
                game.SubmitBotCommand(0, new PotatoBotCommand { Move = Vector2.right, Jump = true });
                for (int i = 0; i < 10; i++) game.StepTraining(0.02f);
                Require(game.Actors[0].Position.y > ArenaGeometry.Floor(game.Actors[0].Position), "Policy jump uses player physics");
                game.ResetTrainingDuel(731);
                Require(game.Actors[0].Position == spawn && game.Rules.Holder == holder, "Seeded resets repeat spawns and holder");
                Require(game.Actors[0].Cooldown == 0 && game.Actors[0].ChargeTime == 0 && !game.Rules.Loose, "Reset clears action state");
                // Drive an actual charge/release through the command API.
                game.Rules.Reset(2);
                game.Rules.BeginRound(0);
                game.Actors[0].Position = new Vector3(0, 0, -3);
                game.Actors[1].Position = new Vector3(4, 0, 3);
                game.SetAgentControl(1, true);
                game.SubmitBotCommand(0, new PotatoBotCommand { HoldThrow = true, Aim = Vector2.left });
                for (int i = 0; i < 25; i++) game.StepTraining(0.02f);
                Require(!game.Rules.Loose && game.Actors[0].ChargeTime > 0, "Policy must charge before throwing");
                game.SubmitBotCommand(0, new PotatoBotCommand { Aim = Vector2.left });
                game.StepTraining(0.02f);
                Require(game.Rules.Loose && game.Rules.Holder == 0 && game.Actors[0].Cooldown > 0, "Policy release obeys responsibility and cooldown");
                for (int i = 0; i < 610 && !game.Rules.Finished; i++) game.StepTraining(0.02f);
                Require(game.Rules.Finished, "Training episode terminates on fuse");
                // Each reset reuses the two actors instead of accumulating GameObjects.
                var actor = game.Actors[0];
                for (int i = 0; i < 20; i++) game.ResetTrainingDuel(900 + i);
                Require(ReferenceEquals(actor, game.Actors[0]), "Training resets reuse actor objects");
                game.ResetTrainingDuel(731, 0.25f);
                Require(Vector3.Distance(game.Actors[0].Position, game.Actors[1].Position) < 3, "Close practice uses nearby spawns");
                game.SetAgentControl(1, true);
                var stationary = game.Actors[1].Position;
                for (int i = 0; i < 50; i++) game.StepTraining(0.02f);
                Require(game.Actors[1].Position == stationary, "Practice opponent stays stationary through the command API");
                // Teammate's arena falls must also work without a graphical view.
                game.BackToLobby();
                game.SetPlayerCount(3);
                typeof(HotPotatoGame).GetMethod("BeginRound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(game, new object[] { 0 });
                for (int i = 0; i < 3; i++) game.SetAgentControl(i, true);
                game.Actors[1].Position = new Vector3(0, -11, 0);
                game.StepTraining(0.02f);
                Require(!game.Actors[1].Alive && game.Rules.Remaining == 2 && game.Rules.Holder == 0 && game.Rules.Running,
                    "Non-holder fall removes only that actor and preserves the round in headless mode");
                game.Actors[0].Position = new Vector3(0, -11, 0);
                game.StepTraining(0.02f);
                Require(game.Rules.Finished && game.CurrentStage == HotPotatoGame.Stage.Finished && game.Actors[2].Alive,
                    "Holder fall leaves one winner without a graphical view");
                game.BackToLobby();
                typeof(HotPotatoGame).GetMethod("BeginRound", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(game, new object[] { 0 });
                game.Actors[0].Position = new Vector3(0, -11, 0);
                game.StepTraining(0.02f);
                Require(game.Rules.Remaining == 2 && !game.Rules.Running && game.CurrentStage == HotPotatoGame.Stage.Intermission,
                    "Holder fall ends the round when multiple survivors remain");
                ValidatePracticeContract();
                Debug.Log("HOT_POTATO_TRAINING_CONTRACT_OK");
            }
            finally { UnityEngine.Object.Destroy(root); }
        }

        static void ValidatePracticeContract()
        {
            var root = new GameObject("Practice contract");
            var arena = root.AddComponent<PotatoTrainingArena>();
            arena.enabled = false; // Drive fixed ticks explicitly in this check.
            try
            {
                InvokeTraining(arena, "Start");
                var game = arena.Game;
                for (int lesson = 1; lesson <= 2; lesson++)
                {
                    InvokeTraining(arena, "ConfigureDuel", lesson, true);
                    Require(game.Rules.Holder == 0, "Every practice lesson starts with learner possession");
                    game.SetAgentControl(0, true);
                    game.SetAgentControl(1, true);
                    game.Actors[0].Position = Vector3.zero;
                    game.Actors[1].Position = Vector3.right * 1.2f;
                    for (int i = 0; i < 12; i++) InvokeTraining(arena, "FixedUpdate");
                    Require(arena.Learner.GetCumulativeReward() == 0, "Practice has no shaping or responsibility reward");
                    int completed = arena.PracticeEpisodes;
                    game.SubmitBotCommand(0, new PotatoBotCommand { Lunge = true, Aim = Vector2.right });
                    for (int i = 0; i < 20 && arena.PracticeEpisodes == completed; i++) InvokeTraining(arena, "FixedUpdate");
                    Require(arena.PracticeEpisodes == completed + 1 && arena.PracticeSuccesses == lesson,
                        "Actual lunge transfer immediately completes practice once");
                }
                InvokeTraining(arena, "ConfigureDuel", 2, true);
                game.Rules.Tick(0.2f);
                Require(game.Rules.Throw(0), "Practice allows a real missed throw");
                int before = arena.PracticeEpisodes;
                InvokeTraining(arena, "FixedUpdate");
                Require(arena.PracticeEpisodes == before, "Throwing without a receiver is not success");
                game.Actors[0].Position = new Vector3(0, -11, 0);
                InvokeTraining(arena, "FixedUpdate");
                Require(arena.PracticeEpisodes == before + 1 && arena.PracticeSuccesses == 2, "Learner fall fails practice");
                InvokeTraining(arena, "ConfigureDuel", 1, true);
                game.Actors[1].Position = new Vector3(0, -11, 0);
                InvokeTraining(arena, "FixedUpdate");
                Require(arena.PracticeEpisodes == before + 2 && arena.PracticeSuccesses == 2,
                    "Winning from opponent fall without a transfer does not pass practice");
                InvokeTraining(arena, "ConfigureDuel", 0, false);
                Require(game.Rules.Running && game.Passes == 0, "Full duel resets practice state");
                Debug.Log("HOT_POTATO_PRACTICE_OK: possession; real transfer; binary reward; missed throw; falls.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void InvokeTraining(PotatoTrainingArena arena, string name, params object[] args)
        {
            typeof(PotatoTrainingArena).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(arena, args);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
