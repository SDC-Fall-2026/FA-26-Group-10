using UnityEngine;
using Unity.InferenceEngine;

namespace HotPotato
{
    // Add to the game object. Unassigned difficulty slots use the scripted baseline.
    [RequireComponent(typeof(HotPotatoGame))]
    [DisallowMultipleComponent]
    public sealed class PotatoModelBots : MonoBehaviour
    {
        public enum Difficulty { Scripted, Experimental, Easy, Normal, Hard }
        public Difficulty difficulty = Difficulty.Scripted;
        public ModelAsset experimental;
        public ModelAsset easy;
        public ModelAsset normal;
        public ModelAsset hard;
        HotPotatoGame game;
        PotatoActor[] cast;
        PotatoLearningAgent[] agents;
        ModelAsset current;

        void Start()
        {
            game = GetComponent<HotPotatoGame>();
            if (experimental == null) experimental = Resources.Load<ModelAsset>("HotPotato/Models/Experimental");
        }

        void Update()
        {
            if (game == null || game.TrainingMode) return;
            ModelAsset selected = difficulty == Difficulty.Easy ? easy :
                difficulty == Difficulty.Normal ? normal : difficulty == Difficulty.Hard ? hard :
                difficulty == Difficulty.Experimental ? experimental : null;
            if (cast == game.Actors && current == selected) return;
            Clear();
            cast = game.Actors;
            current = selected;
            if (selected == null) return;
            agents = new PotatoLearningAgent[cast.Length - 1];
            for (int i = 1; i < cast.Length; i++) agents[i - 1] = PotatoLearningAgent.Create(game, i, selected);
        }

        void Clear()
        {
            if (agents == null) return;
            foreach (var agent in agents)
            {
                if (agent == null) continue;
                agent.gameObject.SetActive(false);
                Destroy(agent.gameObject);
            }
            agents = null;
        }

        void OnDisable() { Clear(); cast = null; current = null; }
    }
}
