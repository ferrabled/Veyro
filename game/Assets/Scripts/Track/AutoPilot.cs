using System;
using System.Collections.Generic;

namespace MotionRunner.Track
{
    /// Plays the track without a player. Used by the menu's attract run - the live game behind the
    /// main menu - so the first thing a player sees is the game being played competently rather
    /// than a screenshot of it.
    ///
    /// Engine-free and stateless between calls, like LaneSelector: "the demo runner walked into a
    /// wall on the menu" is a thing you would only ever catch by watching the menu for a minute,
    /// and the whole decision is arithmetic over cluster lane masks.
    ///
    /// It reads exactly what the player reads - obstacle clusters ahead, in world space - and
    /// answers in the same currency the real input speaks: a target lane and a jump. There is no
    /// privileged information and no collision exemption in here; the attract run simply does not
    /// score, so being wrong costs nothing but looking silly.
    public static class AutoPilot
    {
        /// How far ahead a cluster starts being steered around. 22 m is ~1.6 s at menu speed,
        /// comfortably more than the 0.14 s a lane change takes (LaneSelector.SecondsPerLane), so
        /// the runner commits early and arrives settled rather than sliding through the gap.
        public const float SteerAheadMetres = 22f;

        /// A cluster is only "behind" once the runner is genuinely clear of it: the runner is
        /// 0.3 m deep and a full block 0.45 m, so they still overlap at z = -0.7. Deciding again
        /// any earlier restarts the slide while the runner is still inside the block, which reads
        /// as clipping through it.
        public const float PassedMetres = -1.2f;

        /// Where the hop starts. The jump arc is airborne 0.682 s (RunnerController) and apexes
        /// halfway through, so at ~13 m/s the apex lands ~4.5 m after take-off - which is where
        /// the barrier needs to be.
        public const float JumpAheadMetres = 5f;

        /// Which lane to be in, given the clusters ahead. `clusters` must be ascending in Z, in
        /// world space with the runner at z = 0.
        ///
        /// Only the FIRST cluster still ahead decides. That is what makes the answer stable: it
        /// stays the same cluster from 22 m out until the runner is clear of it, so the runner
        /// picks a gap once and holds it, instead of re-aiming every frame at whichever obstacle
        /// currently looks worst. Clusters are at least MinClusterSpacing (6 m) apart, so there is
        /// always room to solve the next one after this one is behind.
        public static int ChooseLane(IReadOnlyList<ObstacleCluster> clusters, int currentLane)
        {
            if (clusters == null) return currentLane;

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                if (cluster.Z < PassedMetres) continue;
                if (cluster.Z > SteerAheadMetres) break;

                // A cluster that does not block this lane is not a reason to move: a low barrier
                // is jumped, not dodged (see ShouldJump).
                if ((cluster.FullBlockLanes & ObstacleCluster.LaneBit(currentLane)) == 0)
                    return currentLane;

                return NearestOpenLane(cluster, currentLane);
            }

            return currentLane;
        }

        /// Whether to jump this frame, for the lane the runner is committed to. Harmless to
        /// return true repeatedly - RunnerController ignores a jump while airborne - which is what
        /// lets this be a window rather than an edge.
        public static bool ShouldJump(IReadOnlyList<ObstacleCluster> clusters, int lane)
        {
            if (clusters == null) return false;

            for (int i = 0; i < clusters.Count; i++)
            {
                var cluster = clusters[i];
                if (cluster.Z < 0f) continue;
                if (cluster.Z > JumpAheadMetres) break;
                if ((cluster.LowBarrierLanes & ObstacleCluster.LaneBit(lane)) != 0) return true;
            }

            return false;
        }

        /// The best lane out of a cluster that blocks the current one. A cluster can never block
        /// all three (ChunkDefinition.Validate rejects that), so there is always an answer.
        ///
        /// Cost, cheapest first: distance travelled, then whether the lane still needs a jump,
        /// then a hair's preference for the centre - which is where the next cluster is most
        /// likely to be solvable from either side.
        static int NearestOpenLane(in ObstacleCluster cluster, int fromLane)
        {
            int best = fromLane;
            int bestCost = int.MaxValue;

            for (int lane = TrackMetrics.MinLane; lane <= TrackMetrics.MaxLane; lane++)
            {
                int bit = ObstacleCluster.LaneBit(lane);
                if ((cluster.FullBlockLanes & bit) != 0) continue;

                int cost = Math.Abs(lane - fromLane) * 4
                           + ((cluster.LowBarrierLanes & bit) != 0 ? 2 : 0)
                           + (lane == 0 ? 0 : 1);

                if (cost >= bestCost) continue;
                bestCost = cost;
                best = lane;
            }

            return best;
        }
    }
}
