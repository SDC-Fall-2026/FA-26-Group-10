using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace HotPotato.Editor
{
    // Run via Tools > Hot Potato. Batch smoke checks require a separate editor project.
    [InitializeOnLoad]
    public static class PotatoChecks
    {
        const string SmokeKey = "HotPotato.Smoke";
        static int frames;
        static int step;
        static int lastFrame;
        static float pausedFuse;
        static Vector3 beforeMove;
        static Vector3 pausedBomb;
        static Vector3 chargePosition;
        static double chargeStarted;
        static Keyboard keyboard;
        static bool failed;
        static double deadline;

        static PotatoChecks()
        {
            if (SessionState.GetBool(SmokeKey, false)) AttachSmoke();
        }

        static void AttachSmoke()
        {
            frames = step = 0;
            lastFrame = -1;
            failed = false;
            deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update -= SmokeUpdate;
            EditorApplication.update += SmokeUpdate;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        [MenuItem("Tools/Hot Potato/Open Game Scene")]
        public static void OpenScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene("Assets/Scenes/HotPotato.unity");
        }

        [MenuItem("Tools/Hot Potato/Validate Match Rules")]
        public static void ValidateRules()
        {
            var rules = new MatchRules();
            bool rejected = false;
            try { rules.Reset(1); } catch (ArgumentOutOfRangeException) { rejected = true; }
            Require(rejected, "Reject unsupported player counts");
            for (int count = 2; count <= 12; count++)
            {
                rules.Reset(count);
                Require(!rules.BeginRound(-1), "Reject invalid holder");
                Require(rules.BeginRound(0), "First round starts");
                Require(!rules.BeginRound(1), "Do not start two rounds");
                Require(!rules.TryPass(0, 1), "Protect receiver at round start");
                rules.Tick(0.7f);
                float fuse = rules.Fuse;
                Require(!rules.TryPass(1, 2) && !rules.TryPass(0, 0), "Reject nonholder and self passes");
                Require(rules.TryPass(0, 1), "Pass after protection");
                Require(Math.Abs(rules.Fuse - fuse) < 0.0001f, "Passing preserves remaining fuse");
                Require(!rules.TryPass(1, 0), "Prevent immediate tag backs");
                Require(rules.Tick(100) == 1, "Eliminate only current holder");
                Require(rules.Tick(100) == -1 && rules.Remaining == count - 1, "Exactly one elimination per round");
                Require(!rules.BeginRound(1), "Eliminated player cannot hold");
                float previousDuration = rules.RoundDuration;
                for (int round = 2; round <= count - 1; round++)
                {
                    int holder = Array.FindIndex(rules.Alive, alive => alive);
                    Require(rules.BeginRound(holder), "Start successive round");
                    Require(rules.RoundDuration <= previousDuration && rules.RoundDuration >= 3, "Fuse accelerates to a fair minimum");
                    previousDuration = rules.RoundDuration;
                    rules.Tick(0.7f);
                    Require(!rules.TryPass(holder, 1), "Cannot tag an eliminated player");
                    Require(rules.Tick(100) == holder, "Round eliminates holder");
                }
                Require(rules.Finished && rules.Remaining == 1 && rules.Round == count - 1, "Exactly one winner");
                Require(!rules.BeginRound(Array.FindIndex(rules.Alive, alive => alive)), "Finished match cannot restart a round");
                rules.Reset(count);
                Require(rules.Remaining == count && rules.Round == 0 && rules.Holder == -1, "Rematch resets all state");
            }
            rules.Reset(3); rules.BeginRound(0); rules.Tick(0.2f);
            Require(rules.TryPass(0, 1), "Intentional first transfer");
            rules.Tick(0.2f);
            Require(!rules.TryPass(1, 0) && rules.TryPass(1, 2), "Return protection still permits a third-player pass");
            rules.Tick(0.2f);
            Require(!rules.TryPass(2, 0), "Chain passes do not erase earlier immunity");
            rules.Tick(1.2f);
            Require(rules.Throw(2) && rules.Holder == 2 && rules.Loose, "Throw retains responsibility");
            float beforeCatch = rules.Fuse;
            Require(rules.Catch(0) && rules.Holder == 0 && !rules.Loose && rules.Fuse == beforeCatch, "Catch transfers without resetting fuse");
            rules.Tick(0.2f); rules.Throw(0);
            Require(rules.Tick(100) == 0, "Missed throw eliminates responsible thrower");
            Debug.Log("HOT_POTATO_RULES_OK: 2-12 players; fuse; return protection; chain passes; throws; catches; missed-throw responsibility; winner; reset.");
        }

        // CLI: -batchmode -projectPath <isolated copy> -executeMethod HotPotato.Editor.PotatoChecks.RunAll
        // No -quit: the smoke runner exits after exercising Play mode.
        public static void RunAll()
        {
            try
            {
                ValidateRules();
                EditorSceneManager.OpenScene("Assets/Scenes/HotPotato.unity");
                if (Application.isBatchMode) EditorSettings.enterPlayModeOptionsEnabled = false;
                SessionState.SetBool(SmokeKey, true);
                AttachSmoke();
                EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }

        static void SmokeUpdate()
        {
            if (EditorApplication.timeSinceStartup > deadline) { Finish(false, "Runtime smoke timed out"); return; }
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying) return;
            EditorApplication.isPaused = false;
            var game = UnityEngine.Object.FindAnyObjectByType<HotPotatoGame>();
            if (game == null) return;
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (++frames < 12) return;
            frames = 0;
            try
            {
                switch (step++)
                {
                    case 0:
                        PotatoTrainingTools.ValidateTrainingContract();
                        Require(game.Actors.Length == 8 && game.Rules.Remaining == 8, "Lobby creates eight contestants");
                        Require(Resources.Load<Shader>("HotPotato/Clay") != null, "Clay shader included");
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        Application.runInBackground = true;
                        UnityEngine.Random.InitState(5401);
                        Capture(game, "HotPotato-lobby.png");
                        break;
                    case 1:
                        game.StartMatch();
                        Invoke(game, "BeginRound", 0);
                        beforeMove = game.Actors[0].Position;
                        keyboard = InputSystem.AddDevice<Keyboard>();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                        break;
                    case 2:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Require(game.Actors[0].Position.z > beforeMove.z + 0.02f, "W moves the player through Input System; from=" + beforeMove + "; to=" + game.Actors[0].Position + "; paused=" + game.Paused + "; W=" + keyboard.wKey.isPressed + "; dt=" + Time.deltaTime);
                        game.TogglePause();
                        pausedFuse = game.Rules.Fuse;
                        break;
                    case 3:
                        Require(Math.Abs(game.Rules.Fuse - pausedFuse) < 0.0001f, "Pause freezes fuse");
                        game.TogglePause();
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                        break;
                    case 4:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Require(game.Actors[0].Cooldown > 0, "Shift triggers lunge");
                        Capture(game, "HotPotato-gameplay.png");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                        break;
                    case 5:
                        Require(!game.Actors[0].Grounded && game.Actors[0].Position.y > ArenaGeometry.Floor(game.Actors[0].Position), "Space input jumps");
                        game.Actors[0].Position = new Vector3(-0.4f, game.Actors[0].Position.y, -3);
                        game.Actors[0].Velocity = Vector3.zero;
                        game.Actors[0].DashTime = 0;
                        game.Actors[0].Facing = Vector3.forward;
                        chargePosition = game.Actors[0].Position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                        chargeStarted = EditorApplication.timeSinceStartup;
                        break;
                    case 6:
                        if (EditorApplication.timeSinceStartup - chargeStarted < 1.3) { step--; break; }
                        Require(game.AimingThrow && game.Charge > 0.9f, "Holding E charges a throw");
                        Require(ArenaGeometry.Flat(game.Actors[0].Position - chargePosition).magnitude < 0.4f, "Released movement does not drift during a stationary charge; velocity=" + game.Actors[0].Velocity + "; position=" + game.Actors[0].Position);
                        Capture(game, "HotPotato-throw-preview.png");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        break;
                    case 7:
                        Require(game.Rules.Loose || game.Rules.Holder != 0, "Release input launches throw");
                        game.TogglePause();
                        pausedBomb = game.BombPosition;
                        pausedFuse = game.Rules.Fuse;
                        break;
                    case 8:
                        Require(game.BombPosition == pausedBomb && game.Rules.Fuse == pausedFuse, "Pause freezes projectile and fuse");
                        game.TogglePause();
                        break;
                    case 9:
                        ValidateTouch(game);
                        ValidateMechanics(game);
                        // Exercise the actual steering, contact, elimination, and round transitions.
                        foreach (int players in new[] { 2, 6, 8, 10, 12 })
                        {
                            game.BackToLobby(); game.SetPlayerCount(players); game.StartMatch(); Invoke(game, "BeginRound", 0);
                            int budget = 18000;
                            while (game.CurrentStage != HotPotatoGame.Stage.Finished && budget-- > 0)
                            {
                                if (game.CurrentStage == HotPotatoGame.Stage.Intermission)
                                {
                                    int next = Array.FindIndex(game.Rules.Alive, alive => alive);
                                    Invoke(game, "BeginRound", next);
                                }
                                else Invoke(game, "Simulate", 1f / 90, false);
                                foreach (var actor in game.Actors)
                                {
                                    Require(!float.IsNaN(actor.Position.x), "Finite movement");
                                    Require(ArenaGeometry.Flat(actor.Position).magnitude <= HotPotatoGame.ArenaRadius + 4.001f, "Movement stays within the arena's fall boundary");
                                    Require(actor.Position.y >= Mathf.Min(0, ArenaGeometry.Floor(actor.Position)) - 0.01f, "No falling through arena base; off-arena falls are allowed");
                                }
                            }
                            Require(budget > 0 && game.Rules.Remaining == 1, "Simulated match reaches one winner");
                        }
                        game.StartMatch();
                        Require(game.Rules.Remaining == 12 && game.Passes == 0 && game.Actors[0].Alive, "Rematch restores eliminated human");
                        game.BackToLobby(); game.SetPlayerCount(8);
                        break;
                    case 10:
                        var model = Resources.Load<Unity.InferenceEngine.ModelAsset>("HotPotato/Models/Experimental");
                        if (model != null)
                        {
                            var bots = game.GetComponent<PotatoModelBots>() ?? game.gameObject.AddComponent<PotatoModelBots>();
                            bots.experimental = model;
                            bots.difficulty = PotatoModelBots.Difficulty.Experimental;
                            game.StartMatch(); Invoke(game, "BeginRound", 0);
                        }
                        break;
                    case 11:
                        var policies = game.GetComponentsInChildren<PotatoLearningAgent>();
                        if (Resources.Load<Unity.InferenceEngine.ModelAsset>("HotPotato/Models/Experimental") != null)
                        {
                            Require(policies.Length == 7, "Eight-player match creates seven model bots");
                            Require(Array.TrueForAll(policies, p => p.DecisionsReceived > 0), "All model bots execute inference");
                            // Change player count while old policies still await cleanup.
                            game.BackToLobby(); game.SetPlayerCount(2); game.StartMatch(); Invoke(game, "BeginRound", 0);
                        }
                        break;
                    case 12:
                        if (Resources.Load<Unity.InferenceEngine.ModelAsset>("HotPotato/Models/Experimental") != null)
                        {
                            var duelPolicies = game.GetComponentsInChildren<PotatoLearningAgent>();
                            Require(duelPolicies.Length == 1 && duelPolicies[0].DecisionsReceived > 0, "Model bots rebind after rematch/player count change");
                            Debug.Log("HOT_POTATO_INFERENCE_OK: live 8-player and 2-player model control");
                        }
                        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                        Finish(!failed, "Runtime: input, intentional lunges, return protection, jump/landing, ramps/walls, throws/catches/misses, pause, 2/6/8/10/12-player matches, rematch.");
                        break;
                }
            }
            catch (Exception exception) { Finish(false, exception.ToString()); }
        }

        static void Invoke(HotPotatoGame game, string method, params object[] args)
        {
            typeof(HotPotatoGame).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, args);
        }

        static void Duel(HotPotatoGame game)
        {
            game.BackToLobby(); game.SetPlayerCount(2); game.StartMatch(); Invoke(game, "BeginRound", 0);
            game.Rules.Tick(0.2f);
            game.Actors[0].Position = new Vector3(-0.5f, 0, -3);
            game.Actors[1].Position = new Vector3(0.5f, 0, -3);
        }

        static void ValidateMechanics(HotPotatoGame game)
        {
            Duel(game);
            Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 0, "Ordinary contact never passes");
            Invoke(game, "Dash", game.Actors[0], Vector3.forward);
            Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 0 && game.Actors[0].DashDirection == Vector3.forward, "Lunge does not snap to nearby opponent");
            game.Actors[0].Cooldown = 0;
            Invoke(game, "Dash", game.Actors[0], Vector3.right);
            Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 1 && game.Actors[0].EscapeTime > 0 && !game.Actors[1].TagArmed, "Intentional hit passes and creates escape window");
            game.Rules.Tick(0.3f); game.Actors[1].Cooldown = 0;
            Invoke(game, "Dash", game.Actors[1], Vector3.left); Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 1, "Immediate return lunge cannot tag original passer");
            game.Rules.Tick(1.2f); Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 0, "Duel return is possible after protection expires");

            Duel(game);
            var player = game.Actors[0];
            player.JumpBuffer = 0.12f;
            Invoke(game, "MoveActor", player, 1f / 90);
            Require(!player.Grounded && player.Position.y > 0, "Jump lifts feet off floor");
            float peak = 0;
            for (int i = 0; i < 110; i++) { Invoke(game, "MoveActor", player, 1f / 90); peak = Mathf.Max(peak, player.Position.y); }
            Require(peak > 1.25f && player.Grounded && Mathf.Abs(player.Position.y) < 0.01f, "Jump clears deck height and lands");
            player.Position = new Vector3(-3, 0, -2.1f); player.Velocity = Vector3.forward * 3;
            for (int i = 0; i < 70; i++) Invoke(game, "MoveActor", player, 1f / 90);
            Require(Mathf.Abs(player.Position.y - ArenaGeometry.Height) < 0.02f, "Ramp supports walking onto deck");
            player.Position = new Vector3(-1.8f, 0, 1); player.Velocity = Vector3.left * 3;
            for (int i = 0; i < 40; i++) Invoke(game, "MoveActor", player, 1f / 90);
            Require(player.Position.x > -1.91f && player.Position.y < 0.1f, "Cannot walk through vertical platform side");
            player.Position = new Vector3(-3, ArenaGeometry.Height, 2.35f); player.Velocity = Vector3.forward * 2;
            for (int i = 0; i < 90; i++) Invoke(game, "MoveActor", player, 1f / 90);
            Require(player.Grounded && player.Position.y < 0.01f, "Dropping off deck falls to floor");

            Duel(game);
            game.Actors[1].Position = new Vector3(0.5f, 1.5f, -3);
            Invoke(game, "Dash", game.Actors[0], Vector3.right); Invoke(game, "ResolveContacts");
            Require(game.Rules.Holder == 0, "Jump can evade a ground lunge");

            Duel(game);
            game.Actors[0].Position = new Vector3(-1.8f, 0, -3);
            game.Actors[1].Position = new Vector3(0.4f, 0, -3);
            float fuse = game.Rules.Fuse;
            Invoke(game, "TryThrow", game.Actors[0], Vector3.right, 0f);
            Require(game.Rules.Loose && game.Rules.Holder == 0, "Throw launches without releasing responsibility");
            for (int i = 0; i < 70 && game.Rules.Loose; i++) Invoke(game, "StepBomb", 1f / 90);
            Require(game.Rules.Holder == 1 && !game.Rules.Loose && game.Rules.Fuse == fuse, "Ballistic hit transfers to receiver");

            Duel(game);
            game.Actors[0].Position = new Vector3(-1.8f, 0, -3);
            game.Actors[1].Position = new Vector3(0.4f, 1.7f, -3);
            Invoke(game, "TryThrow", game.Actors[0], Vector3.right, 0f);
            for (int i = 0; i < 40 && game.Rules.Loose; i++) Invoke(game, "StepBomb", 1f / 90);
            Require(game.Rules.Holder == 0 && game.Rules.Loose, "Low throw passes underneath airborne player");

            Duel(game);
            game.Actors[1].Position = new Vector3(4, 0, -2);
            Invoke(game, "TryThrow", game.Actors[0], Vector3.left, 0f);
            for (int i = 0; i < 250 && game.Rules.Loose; i++) Invoke(game, "StepBomb", 1f / 90);
            Require(game.Rules.Holder == 0, "Missing never assigns responsibility to nearest player");
            Require(game.BombPosition.y >= 0.22f && ArenaGeometry.Flat(game.BombPosition).magnitude <= HotPotatoGame.ArenaRadius + 0.01f, "Loose bomb remains recoverable in arena");
            if (game.Rules.Loose)
            {
                game.Actors[0].Position = new Vector3(game.BombPosition.x, ArenaGeometry.Floor(game.BombPosition), game.BombPosition.z);
                Invoke(game, "StepBomb", 1f / 90);
                Require(!game.Rules.Loose && game.Rules.Holder == 0, "Thrower can retrieve a missed throw");
            }
            Debug.Log("HOT_POTATO_MECHANICS_OK: no auto-tag or aim lock; return window; jumps, ramps, walls, drops; ballistic hit and missed-ball retrieval.");
        }

        static void ValidateTouch(HotPotatoGame game)
        {
            var touch = InputSystem.AddDevice<Touchscreen>();
            var input = new PotatoInput();
            try
            {
                Vector2 left = new Vector2(Screen.width * 0.1f, Screen.height * 0.3f);
                Vector2 right = new Vector2(Screen.width * 0.7f, Screen.height * 0.4f);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = left });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                Require(input.StickActive, "Touch starts movement stick");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Moved, position = left + Vector2.right * 100 });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                Require(input.Move.x > 0.5f, "Touch drag moves horizontally");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2, phase = UnityEngine.InputSystem.TouchPhase.Began, position = right });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                Require(input.PrimaryHeld && input.StickActive, "Aim and movement support simultaneous touches");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2, phase = UnityEngine.InputSystem.TouchPhase.Canceled, position = right });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                Require(!input.TagPressed && !input.ThrowReleased && !input.PrimaryHeld, "Canceled touch never fires an action");
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 3, phase = UnityEngine.InputSystem.TouchPhase.Began, position = right });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 3, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = right });
                InputSystem.Update(); input.Read(game.View, game.Actors[0].Position, true);
                Require(input.TagPressed && !input.ThrowReleased, "Quick touch lunges instead of throwing");
                Debug.Log("HOT_POTATO_TOUCH_OK: movement, simultaneous aim, canceled gestures, quick tap.");
            }
            finally { InputSystem.RemoveDevice(touch); }
        }

        static void Capture(HotPotatoGame game, string path)
        {
            var camera = game.View.Camera;
            var canvas = game.GetComponentInChildren<Canvas>();
            var target = new RenderTexture(1280, 800, 24);
            var image = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var previousMode = canvas.renderMode;
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            canvas.renderMode = previousMode;
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(target);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HOT_POTATO_CHECK_FAILED: " + message);
        }

        static void Finish(bool success, string detail)
        {
            SessionState.SetBool(SmokeKey, false);
            EditorApplication.update -= SmokeUpdate;
            Application.logMessageReceived -= OnLog;
            File.WriteAllText("HotPotato-smoke.txt", (success ? "PASS\n" : "FAIL\n") + detail);
            Debug.Log("HOT_POTATO_SMOKE_" + (success ? "OK: " : "FAILED: ") + detail);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
