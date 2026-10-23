# Hot Potato reinforcement learning

This is the first experiment: one PPO learner versus the existing scripted bot in a two-player duel. Training uses the actual movement, collision, jump, throw, cooldown, and bomb-responsibility rules. The training scene omits rendering and HUD construction. It is not included in the normal game's build scenes.

## Setup and run (Windows)

Use Unity **6000.6.6f1**. The Unity dependency is **ML-Agents 4.1.0**. The published Python trainer is **mlagents 1.1.0**, following the [4.1 installation guide](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Installation.html). These package version numbers are intentionally different.

Run commands from the project root:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Setup-HotPotatoTraining.ps1
powershell -ExecutionPolicy Bypass -File Tools/Build-HotPotatoTraining.ps1
powershell -ExecutionPolicy Bypass -File Tools/Train-HotPotato.ps1 -Smoke -RunId duel-smoke
```

Setup downloads a project-local Python 3.10.12 using uv; it does not replace system Python. The trainer uses CPU PyTorch 2.2.2. This avoids assuming CUDA compatibility, particularly on newer GPUs. `setuptools` is pinned because this trainer still imports `pkg_resources`.

`requirements.txt` records the direct constraints; setup installs the fully pinned `requirements-lock.txt` so teammates get the tested dependency versions.

Build and gameplay-check commands share the isolated project under `Temp/HotPotatoChecks/Project`: run them sequentially. Building does not close or replace your open editor scene. The first package import/build can take several minutes.

The smoke configuration runs 4,096 policy decisions to validate training and ONNX export. It is not enough evidence of a useful difficulty level.

For a longer experiment:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Train-HotPotato.ps1 -RunId duel-v1
# Continue the same experiment after interruption:
powershell -ExecutionPolicy Bypass -File Tools/Train-HotPotato.ps1 -RunId duel-v1 -Resume
./.venv-ml/Scripts/tensorboard.exe --logdir Training/results
```

`duel.yaml` runs 250,000 decisions, saving checkpoints every 25,000. Use distinct run IDs and seeds for independent experiments; the scripts do not overwrite existing runs. Training outputs and Python environments are ignored by Git.

The direct-duel baseline did not learn useful passing in its first run; see [EXPERIMENTS.md](EXPERIMENTS.md) for measured results. A staged experiment is also available:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Train-HotPotato.ps1 -Curriculum -RunId curriculum-v2
```

It starts with nearby stationary opponents, progresses to distant stationary opponents, and finally enables the scripted opponent. In both stationary lessons, the learner always starts holding the bomb. An episode ends immediately on its first successful transfer, or fails on elimination/fuse expiry. Its total reward is exactly 1 for transfer success and 0 for failure, with no approach shaping or responsibility cost. Lessons advance when the trainer reward buffer contains at least 100 episodes and its mean exceeds 0.8 (over 80% transfer success); smoothing is disabled. `Duel/PracticeLevel` is 2, 1, or 0 respectively. Monitor `Practice/TransferSuccess` for these lessons. `Duel/WinRate` is recorded only for full duels; practice success does not establish strength against a moving opponent. After promotion to level 0, randomized initial possession, scripted opposition, and full-duel rewards apply.

Use a fresh run ID such as `curriculum-v2` after this lesson/reward change; do not resume `curriculum-v1`. The 250,000-decision budget is an initial experiment, not a guarantee of reaching all lessons. Falling off the arena fails practice, and an opponent falling without a transfer is also a practice failure.

For interactive training, open **Tools > Hot Potato > Open Training Scene**, run `mlagents-learn` with the config but without `--env`, then press Play. This scene has no camera; use training statistics rather than the Game view to inspect progress. The normal game remains in `Assets/Scenes/HotPotato.unity`.

## Agent interface (HotPotatoV1)

- Four continuous actions: move X/Z and aim X/Z.
- Discrete branch 0: idle/release, lunge, or hold throw.
- Discrete branch 1: no jump or jump.
- Decision interval: 0.1 seconds of simulation time.
- Movement and charging persist between decisions; jump/lunge impulses are consumed once.
- Charging takes actual simulation time. Releasing after the normal throw threshold launches the bomb; a short hold behaves like a quick tap.
- Cooldowns, immunity, collision, and fuse ownership are enforced by `HotPotatoGame`, never by the model.

There are **181 numeric observations**: 30 self/bomb/match values, eight local terrain-height samples, and eleven opponent slots with thirteen values each. Opponent slots use actor order excluding self; absent or eliminated players have zero-filled slots and an alive mask. Values use fixed scales. Observations include public positions/velocities and cooldowns; they do not reveal future bot decisions or random choices.

The interface accommodates 2-12 players, but a model trained only on duels is not validated for crowded matches. Retrain with multiple player counts before claiming general multiplayer competence. Any observation/action schema change needs a new behavior name and new compatible models.

Training uses 0.02-second fixed ticks, subdivided by the same maximum simulation step as gameplay. Changing `time_scale` speeds wall-clock training without increasing those steps. Live bots use the same 0.1-second decision cadence.

## Episodes and rewards

Outside stationary practice, one episode is one duel with a 12-second fuse, ending when a player is eliminated (including falls). The arena resets with seeded, randomized spawn angles/radii and initial possession. State resets reuse the actors.

- Win: +1.
- Elimination: -1.
- Responsibility for the bomb: -0.01 per simulated second (including a missed throw); at most -0.12 per duel.
- No pass-count reward, which could encourage pointless repeated transfers.

In the full-duel lesson only, the optional curriculum adds `gamma * potential(next) - potential(previous)` at the 0.1-second decision cadence. Potential is zero without responsibility; with responsibility it is minus half the normalized distance to the receiver (or to one's loose bomb). Terminal potential is zero. The shaping discount and config discount must both remain 0.995. This supplies approach feedback without a repeatable per-pass bonus. Native evaluation always disables practice and shaping.

Monitor `Duel/WinRate` separately from cumulative reward. Reward improvement alone does not establish better gameplay.

## Import and evaluate a model

The trainer exports `Training/results/<run-id>/HotPotatoV1.onnx`. Copy the chosen export to `Assets/Resources/HotPotato/Models/Experimental.onnx`, let Unity import it, and rebuild the training executable. Keep only selected models in Assets, not every checkpoint.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Import-HotPotatoModel.ps1 -RunId duel-smoke
powershell -ExecutionPolicy Bypass -File Tools/Build-HotPotatoTraining.ps1
powershell -ExecutionPolicy Bypass -File Tools/Evaluate-HotPotato.ps1 -Episodes 200 -Seed 700000
powershell -ExecutionPolicy Bypass -File Tools/Evaluate-HotPotato.ps1 -Episodes 200 -Seed 700000 -Scripted
```

Evaluation runs the exported model through Unity's inference engine against the scripted opponent. The second command measures scripted-versus-scripted on the same spawn seeds. Results and logs are under `Training/evaluation`. Seeds used for evaluation should be outside the training episode-seed range. No training or reward updates to model weights occur during evaluation.

To play against the imported export, select the **Hot Potato Tag** scene object and choose **Experimental** in the **Potato Model Bots** component's difficulty field. Its Experimental slot automatically loads `Resources/HotPotato/Models/Experimental` on Play unless you assign another model. Scripted is the default. Easy/Normal/Hard are separate model slots; unassigned slots use the scripted baseline. These are Inspector settings for this first experiment, not new lobby controls.

Select **2 players** for human-versus-model duels; the human remains the yellow character. With more players, every opponent uses the selected model. To compare another candidate, drag its ONNX asset into the component's **Experimental** model slot.

Keep candidates separate while someone is playtesting:

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Import-HotPotatoModel.ps1 -RunId curriculum-v1 -AssetName Curriculum
powershell -ExecutionPolicy Bypass -File Tools/Build-HotPotatoTraining.ps1 -EvaluationModel Assets/Resources/HotPotato/Models/Curriculum.onnx
powershell -ExecutionPolicy Bypass -File Tools/Evaluate-HotPotato.ps1 -Episodes 200 -Seed 700000
```

The build override replaces the evaluation model only inside the isolated build project. It leaves the normal scene's Experimental model untouched. The evaluation directory records the selected model's SHA-256 alongside results.

## Difficulty and team workflow

Do not label a smoke model Easy or an older checkpoint Hard without measuring it. Evaluate several checkpoints against the same opponent set and seeds, then playtest them. Measure win rate, successful throws, last-second transfers, and whether behavior is fun and readable. Train multiple seeds to distinguish improvement from luck.

Future work: mixed-opponent training, duel self-play, variable player counts, and calibrated difficulty models. Free-for-all training needs its own opponent/episode design; merely assigning twelve team IDs is not a finished self-play setup.

The gameplay integration is `PotatoBotCommand` plus `HotPotatoGame.Agents.cs`. Teammates can change rendering and UI independently. Changes to physics, arena geometry, throw speed, observations, or rules require rebuilding the training player and reevaluating existing models. Keep experiment metadata (commit, seed, configuration, metrics) beside each promoted model.

References: [ML-Agents 4.1 overview](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/index.html), [training workflow](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Training-ML-Agents.html), [agent design](https://docs.unity3d.com/Packages/com.unity.ml-agents@4.1/manual/Learning-Environment-Design-Agents.html).
