using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// The park visuals for one ChunkDefinition, plus the per-instance state a chunk needs
    /// while it is live (which coins are still there).
    ///
    /// Views are pooled per chunkId by TrackDirector, so the geometry for a given definition is
    /// built once per session and then only moved and reset - nothing is created or destroyed
    /// while the player is running.
    public sealed class ChunkView : MonoBehaviour
    {
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
            Art.ParkChunkArt.Build(transform, definition);
            BuildCoins(definition.Coins);
        }

        void BuildCoins(CoinPlacement[] coins)
        {
            _coinObjects = new GameObject[coins.Length];
            _coinTaken = new bool[coins.Length];

            for (int i = 0; i < coins.Length; i++)
            {
                var box = TrackGeometry.Coin(coins[i], 0f);
                var coin = Primitive(PrimitiveType.Cylinder, "Coin_" + i, Art.ParkTheme.Gold);
                coin.localScale = new Vector3(0.60f, 0.075f, 0.60f);
                coin.localPosition = new Vector3(box.CenterX, box.CenterY, box.CenterZ);
                coin.localRotation = Quaternion.Euler(90f, 0f, 0f);
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
            if (collider != null) { if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider); }
            go.GetComponent<Renderer>().sharedMaterial = RuntimeMaterials.Shared(color);
            go.transform.SetParent(transform, false);
            return go.transform;
        }
    }
}
