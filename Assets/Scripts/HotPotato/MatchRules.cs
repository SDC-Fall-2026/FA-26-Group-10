using System;

namespace HotPotato
{
    // Engine-independent match rules. Passing never refreshes the fuse.
    public sealed class MatchRules
    {
        public const float PassProtection = 0.18f;
        public const float ReturnProtection = 1.15f;
        public bool Loose { get; private set; }
        float[] immunity;
        public bool[] Alive { get; private set; }
        public int Holder { get; private set; } = -1;
        public int Round { get; private set; }
        public int Remaining { get; private set; }
        public float Fuse { get; private set; }
        public float RoundDuration { get; private set; }
        public float Protection { get; private set; }
        public bool Running { get; private set; }
        public bool Finished => Remaining == 1;

        public void Reset(int players)
        {
            if (players < 2 || players > 12) throw new ArgumentOutOfRangeException(nameof(players));
            Alive = new bool[players];
            immunity = new float[players];
            Loose = false;
            for (int i = 0; i < players; i++) Alive[i] = true;
            Remaining = players;
            Round = 0;
            Holder = -1;
            Fuse = 0;
            Protection = 0;
            Running = false;
        }

        public bool BeginRound(int holder)
        {
            if (Alive == null || Running || Finished || holder < 0 || holder >= Alive.Length || !Alive[holder]) return false;
            Round++;
            RoundDuration = Math.Max(3f, 12f * (float)Math.Pow(0.83, Round - 1));
            Fuse = RoundDuration;
            Protection = PassProtection;
            Array.Clear(immunity, 0, immunity.Length);
            Loose = false;
            Holder = holder;
            Running = true;
            return true;
        }

        public bool TryPass(int from, int to)
        {
            if (Loose || from != Holder || !CanReceive(to)) return false;
            Transfer(to);
            return true;
        }

        public float Immunity(int player) => immunity[player];

        public bool CanReceive(int to) => Running && Protection <= 0 && to >= 0 && to < Alive.Length &&
            to != Holder && Alive[to] && immunity[to] <= 0;

        void Transfer(int to)
        {
            immunity[Holder] = ReturnProtection;
            Holder = to;
            Protection = PassProtection;
            Loose = false;
        }

        public bool Throw(int from)
        {
            if (!Running || Loose || from != Holder || Protection > 0) return false;
            Loose = true; // Holder remains responsible until a different player receives it.
            return true;
        }

        public bool Catch(int to)
        {
            if (!Running || !Loose || to < 0 || to >= Alive.Length || !Alive[to]) return false;
            if (to == Holder) { Loose = false; return true; }
            if (!CanReceive(to)) return false;
            Transfer(to);
            return true;
        }

        // Returns the eliminated player, or -1. Exactly one elimination per round.
        public int Tick(float deltaTime)
        {
            if (!Running || deltaTime <= 0) return -1;
            Protection = Math.Max(0, Protection - deltaTime);
            for (int i = 0; i < immunity.Length; i++) immunity[i] = Math.Max(0, immunity[i] - deltaTime);
            Fuse = Math.Max(0, Fuse - deltaTime);
            if (Fuse > 0) return -1;
            int eliminated = Holder;
            Alive[eliminated] = false;
            Remaining--;
            Holder = -1;
            Loose = false;
            Running = false;
            return eliminated;
        }
    }
}
