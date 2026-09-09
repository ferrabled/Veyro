using System.Collections.Generic;
using MotionRunner.Gameplay;
using MotionRunner.Track;

namespace MotionRunner.Menu
{
    /// Turns the live track into the flat, world-space list of obstacle clusters AutoPilot reads.
    ///
    /// Two things it exists for. One, the translation: chunks store clusters at chunk-relative z
    /// and the pilot thinks in metres ahead of the runner, and doing that conversion at the call
    /// site is how the two drift apart. Two, the allocation: this runs every frame the menu is up,
    /// and the list plus its comparer are reused rather than rebuilt, so an idle menu allocates
    /// nothing per frame - the same discipline TrackDirector applies to chunk views.
    public sealed class ChunkClusters
    {
        /// Nothing past this matters: AutoPilot.SteerAheadMetres is 22, and the extra margin keeps
        /// the list stable for a frame or two rather than having clusters pop in exactly at the
        /// decision boundary.
        const float HorizonMetres = 40f;

        readonly TrackDirector _director;
        readonly List<ObstacleCluster> _ahead = new List<ObstacleCluster>(16);

        public ChunkClusters(TrackDirector director) => _director = director;

        /// Every cluster between the runner and the horizon, ascending in z. The list is the
        /// object's own and is overwritten on the next call - AutoPilot only reads it inside the
        /// frame that asked for it.
        public IReadOnlyList<ObstacleCluster> Refresh()
        {
            _ahead.Clear();
            if (_director == null) return _ahead;

            var chunks = _director.ActiveChunks;
            for (int c = 0; c < chunks.Count; c++)
            {
                var chunk = chunks[c];
                if (chunk.StartZ > HorizonMetres) continue;
                if (chunk.EndZ < AutoPilot.PassedMetres) continue;

                var clusters = chunk.Definition.Clusters;
                for (int i = 0; i < clusters.Length; i++)
                {
                    float z = chunk.StartZ + clusters[i].Z;
                    if (z < AutoPilot.PassedMetres || z > HorizonMetres) continue;
                    _ahead.Add(new ObstacleCluster(z, clusters[i].FullBlockLanes, clusters[i].LowBarrierLanes));
                }
            }

            // TrackDirector keeps ActiveChunks in the order it laid them down and every chunk's
            // clusters are already sorted, so the concatenation is sorted too - but AutoPilot's
            // contract is "ascending in Z" and a recycle that ever reordered the list would break
            // it silently, one wrong lane at a time. Insertion sort over a handful of nearly
            // sorted entries costs nothing and makes the contract hold by construction.
            for (int i = 1; i < _ahead.Count; i++)
            {
                var item = _ahead[i];
                int j = i - 1;
                while (j >= 0 && _ahead[j].Z > item.Z)
                {
                    _ahead[j + 1] = _ahead[j];
                    j--;
                }
                _ahead[j + 1] = item;
            }

            return _ahead;
        }
    }
}
