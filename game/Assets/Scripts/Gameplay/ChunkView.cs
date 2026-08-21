using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// The greybox visuals for one ChunkDefinition, plus the per-instance state a chunk needs
    /// while it is live (which coins are still there).
    ///
    /// Views are pooled per chunkId by TrackDirector, so the geometry for a given definition is
    /// built once per session and then only moved and reset - nothing is created or destroyed
    /// while the player is running.
    public sealed class ChunkView : MonoBehaviour
    {
        const float DeckSegmentLength = 6f;
        const float DeckSegmentGap = 0.12f;
        const float TunnelArchSpacing = 6.5f;

        static readonly Color DeckA = new Color(0.22f, 0.25f, 0.32f);
        static readonly Color DeckB = new Color(0.28f, 0.32f, 0.40f);
        static readonly Color BridgeDeck = new Color(0.31f, 0.26f, 0.21f);
        static readonly Color BridgeRail = new Color(0.55f, 0.46f, 0.30f);
        static readonly Color LaneStripe = new Color(0.46f, 0.52f, 0.62f);
        static readonly Color TunnelFrame = new Color(0.17f, 0.19f, 0.27f);
        static readonly Color LowBarrierColor = new Color(0.96f, 0.66f, 0.16f);
        static readonly Color FullBlockColor = new Color(0.82f, 0.21f, 0.23f);
        static readonly Color CoinColor = new Color(1f, 0.86f, 0.22f);

        ChunkDefinition _definition;
        GameObject[] _coinObjects;

        /// True once coin i has been picked up during the chunk's current pass.
        bool[] _coinTaken;

        public ChunkDefinition Definition => _definition;
        public float StartZ { get; private set; }
        public float EndZ => StartZ + _definition.Length;

        public int ObstacleCount => _definition.Obstacles.Length;
        public int CoinCount => _definition.Coins.Length;

        public Aabb ObstacleBounds(int index) =>
            TrackGeometry.Obstacle(_definition.Obstacles[index], StartZ);

        public Aabb CoinBounds(int index) =>
            TrackGeometry.Coin(_definition.Coins[index], StartZ);

        public bool IsCoinAvailable(int index) => !_coinTaken[index];

        public void TakeCoin(int index)
        {
            _coinTaken[index] = true;
            _coinObjects[index].SetActive(false);
        }

        public static ChunkView Create(ChunkDefinition definition, Transform parent)
        {
            var go = new GameObject("Chunk_" + definition.ChunkId);
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<ChunkView>();
            view.Build(definition);
            return view;
        }

        public void PlaceAt(float startZ)
        {
            StartZ = startZ;
            transform.localPosition = new Vector3(0f, 0f, startZ);
        }

        public void Translate(float deltaZ)
        {
            StartZ += deltaZ;
            transform.localPosition = new Vector3(0f, 0f, StartZ);
        }

        /// Puts back every coin. Called when the view is taken out of the pool, not per frame.
        public void PrepareForReuse()
        {
            for (int i = 0; i < _coinTaken.Length; i++)
            {
                if (!_coinTaken[i]) continue;
                _coinTaken[i] = false;
                _coinObjects[i].SetActive(true);
            }
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        void Build(ChunkDefinition definition)
        {
            _definition = definition;
            bool bridge = definition.EntryType == ChunkEdge.Bridge || definition.ExitType == ChunkEdge.Bridge;

            BuildDeck(definition.Length, bridge);
            BuildLaneStripes(definition.Length);
            if (bridge) BuildRailings(definition.Length);
            if (definition.HasTag(ChunkTag.Tunnel)) BuildTunnel(definition.Length);

            for (int i = 0; i < definition.Obstacles.Length; i++) BuildObstacle(definition.Obstacles[i]);
            BuildCoins(definition.Coins);
        }

        void BuildDeck(float length, bool bridge)
        {
            int segments = Mathf.CeilToInt(length / DeckSegmentLength);
            for (int i = 0; i < segments; i++)
            {
                float z0 = i * DeckSegmentLength;
                float segmentLength = Mathf.Min(DeckSegmentLength, length - z0);
                var color = bridge ? BridgeDeck : (i % 2 == 0 ? DeckA : DeckB);

                var deck = Primitive(PrimitiveType.Cube, "Deck_" + i, color);
                deck.localScale = new Vector3(
                    TrackMetrics.RoadHalfWidth * 2f,
                    TrackMetrics.DeckThickness,
                    Mathf.Max(0.1f, segmentLength - DeckSegmentGap));
                deck.localPosition = new Vector3(0f, -TrackMetrics.DeckThickness * 0.5f, z0 + segmentLength * 0.5f);
            }
        }

        void BuildLaneStripes(float length)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var stripe = Primitive(PrimitiveType.Cube, "LaneStripe", LaneStripe);
                stripe.localScale = new Vector3(0.07f, 0.02f, length - 0.3f);
                stripe.localPosition = new Vector3(side * TrackMetrics.LaneWidth * 0.5f, 0.005f, length * 0.5f);
            }
        }

        void BuildRailings(float length)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                var rail = Primitive(PrimitiveType.Cube, "Railing", BridgeRail);
                rail.localScale = new Vector3(0.14f, 0.9f, length - 0.2f);
                rail.localPosition = new Vector3(side * (TrackMetrics.RoadHalfWidth + 0.12f), 0.45f, length * 0.5f);
            }
        }

        void BuildTunnel(float length)
        {
            int arches = Mathf.Max(1, Mathf.FloorToInt(length / TunnelArchSpacing));
            for (int i = 0; i < arches; i++)
            {
                float z = (i + 0.5f) * (length / arches);

                var beam = Primitive(PrimitiveType.Cube, "ArchBeam", TunnelFrame);
                beam.localScale = new Vector3(TrackMetrics.RoadHalfWidth * 2f + 0.8f, 0.35f, 0.45f);
                beam.localPosition = new Vector3(0f, 3.1f, z);

                for (int side = -1; side <= 1; side += 2)
                {
                    var post = Primitive(PrimitiveType.Cube, "ArchPost", TunnelFrame);
                    post.localScale = new Vector3(0.3f, 3.1f, 0.45f);
                    post.localPosition = new Vector3(side * (TrackMetrics.RoadHalfWidth + 0.25f), 1.55f, z);
                }
            }
        }

        void BuildObstacle(ObstaclePlacement placement)
        {
            bool full = placement.Kind == ObstacleKind.FullBlock;
            var box = TrackGeometry.Obstacle(placement, 0f);

            var obstacle = Primitive(PrimitiveType.Cube, placement.Kind.ToString(), full ? FullBlockColor : LowBarrierColor);
            obstacle.localScale = new Vector3(box.HalfX * 2f, box.HalfY * 2f, box.HalfZ * 2f);
            obstacle.localPosition = new Vector3(box.CenterX, box.CenterY, box.CenterZ);
        }

        void BuildCoins(CoinPlacement[] coins)
        {
            _coinObjects = new GameObject[coins.Length];
            _coinTaken = new bool[coins.Length];

            for (int i = 0; i < coins.Length; i++)
            {
                var box = TrackGeometry.Coin(coins[i], 0f);
                var coin = Primitive(PrimitiveType.Cube, "Coin_" + i, CoinColor);
                coin.localScale = new Vector3(0.42f, 0.42f, 0.42f);
                coin.localPosition = new Vector3(box.CenterX, box.CenterY, box.CenterZ);
                coin.localRotation = Quaternion.Euler(0f, 45f, 45f);   // reads as a gem, costs nothing
                _coinObjects[i] = coin.gameObject;
            }
        }

        /// CreatePrimitive also attaches a Collider. We resolve collisions ourselves against
        /// deterministic AABBs (CLAUDE.md rule 4), so the colliders are dead weight - and a
        /// PhysX body per obstacle is exactly the sort of thing that ruins the memory profile.
        Transform Primitive(PrimitiveType type, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Shared(color);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
