using System.Collections.Generic;
using UnityEngine;

namespace MotionRunner.Gameplay
{
    /// Recycles a fixed pool of road segments moving toward the camera —
    /// the seed of the chunk system (T-003) and the "world moves, player doesn't" illusion.
    public sealed class RoadScroller : MonoBehaviour
    {
        const int SegmentCount = 12;
        const float SegmentLength = 8f;
        const float RoadWidth = RunnerController.LaneWidth * 3f;

        public float Speed = 12f;

        readonly List<Transform> _segments = new();

        void Start()
        {
            var matEven = MotionRunner.Core.RuntimeMaterials.Lit(new Color(0.22f, 0.25f, 0.32f));
            var matOdd = MotionRunner.Core.RuntimeMaterials.Lit(new Color(0.28f, 0.32f, 0.40f));

            for (int i = 0; i < SegmentCount; i++)
            {
                var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                seg.name = $"RoadSegment_{i}";
                seg.transform.SetParent(transform, false);
                seg.transform.localScale = new Vector3(RoadWidth, 0.2f, SegmentLength - 0.15f);
                seg.transform.position = new Vector3(0f, -0.1f, i * SegmentLength);
                seg.GetComponent<Renderer>().sharedMaterial = (i % 2 == 0) ? matEven : matOdd;

                _segments.Add(seg.transform);
            }
        }

        void Update()
        {
            float dz = Speed * Time.deltaTime;
            foreach (var seg in _segments)
            {
                var p = seg.position;
                p.z -= dz;
                if (p.z < -SegmentLength * 1.5f) p.z += SegmentCount * SegmentLength;
                seg.position = p;
            }
        }
    }
}
