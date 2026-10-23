# Training experiments

## duel-v1: direct PPO against the scripted opponent

- Unity ML-Agents 4.1.0, Python trainer 1.1.0, CPU PyTorch 2.2.2.
- Configuration: `duel.yaml`; seed 12345; 250,080 decisions including the final completed episode.
- Training duration reported by the trainer: about 549 seconds.
- Export SHA-256: `33D1B966D11737D40C69F94D8D05EF7CC89371776BC463DF8A1B6CE15D01D6D0`.
- Training summaries: [duel-v1-metrics.csv](duel-v1-metrics.csv).

Held-out evaluation used Unity inference, 200 full duels, and consecutive episode seeds starting at 700000. Neither practice opponents nor reward shaping were used.

| Controller in seat 0 | Wins against the scripted seat-1 opponent |
| --- | --- |
| Scripted baseline | 97 / 200 (48.5%) |
| Exported duel-v1 model | 14 / 200 (7.0%) |

This model is substantially worse than the scripted baseline and is not a calibrated difficulty model. The run verifies the training/export/inference pipeline, not successful gameplay learning. Passing was rare in training; the last 5,000-decision summary had zero mean successful passes. Terminal win/loss rewards alone did not provide sufficient learning in this experiment.

## curriculum-v1: approach shaping and a stationary practice opponent

- Configuration: `curriculum.yaml`; seed 12345; 250,081 decisions.
- Training duration reported by the trainer: about 597 seconds.
- Export SHA-256: `6C96FDAAC1CF42F950A95C211D0081BEAC19A2492C5EC6BFF3D656A94ED4F9CF`.
- Imported separately as `Assets/Resources/HotPotato/Models/Curriculum.onnx`.
- Training summaries: [curriculum-v1-metrics.csv](curriculum-v1-metrics.csv).

This configuration starts near a stationary opponent, then is intended to increase distance and finally enable the normal scripted opponent. It adds discounted potential-difference shaping for approaching the receiver or retrieving one's own loose bomb.

The run stayed in `CloseStationaryPass` throughout; it did not meet the promotion criterion. Its final summary reported 69.0% practice wins and 0.143 successful passes per episode. Initial bomb ownership is randomized, so the stationary opponent loses automatically in many episodes. Practice win rate cannot establish skill against a moving opponent.

Native Unity evaluation against the full scripted opponent used the same 200 episode seeds starting at 700000:

| Controller in seat 0 | Wins against the scripted seat-1 opponent |
| --- | --- |
| Scripted baseline | 97 / 200 (48.5%) |
| Direct PPO (`Experimental.onnx`) | 14 / 200 (7.0%) |
| Curriculum PPO (`Curriculum.onnx`) | 20 / 200 (10.0%) |

The curriculum candidate won six more games on this seed set, but remains substantially below the scripted baseline. This single comparison does not establish reliable improvement. The full evaluation result and model fingerprint are retained locally under `Training/evaluation/model-20261020-011251`.

The next learning experiment should focus on reliable transfers: begin practice episodes with the learner holding the bomb, measure transfer success separately from match wins, and validate that the policy masters that task before increasing opponent movement or adding more players. These changes are proposed follow-up work, not properties of the current exports.

Both exports are experimental candidates, not calibrated Easy/Normal/Hard models. For human playtesting, open the normal scene, set Potato Model Bots to Experimental, and assign the candidate ONNX asset to its Experimental slot. Select two players for a duel.


## Transfer-based curriculum revision (not yet trained)

Stationary lessons now force initial learner possession and terminate on the first successful transfer. Total practice reward is binary: 1 for a transfer while alive, 0 for elimination without one. Approach shaping and responsibility penalties are disabled in these lessons. The trainer therefore promotes on transfer success directly: an unsmoothed mean above 0.8 over its reward buffer of at least 100 episodes. Missed throws and opponent falls without a transfer do not count.

Level 0 still trains complete duels with randomized possession, the scripted opponent, and the existing duel rewards/shaping. Arena falls remain enabled throughout. This revision does not add a separate edge-avoidance lesson or change the model observation/action interface. Previous exports remain usable but the earlier win rates were measured before the falling mechanic.

Start a new run (`curriculum-v2`) after rebuilding. No improved win rate is claimed until that candidate is trained and evaluated against a newly measured scripted baseline.
