using UnityEngine;

namespace MotionRunner.Core
{
    /// Colored materials cloned from the shipped URP-Lit base
    /// (see ProjectSetup.CreateBaseMaterial for why a Resources asset is required).
    public static class RuntimeMaterials
    {
        static Material _baseLit;
        static Material BaseLit =>
            _baseLit != null ? _baseLit : _baseLit = Resources.Load<Material>("Materials/PrimitiveLit");

        public static Material Lit(Color color)
        {
            var m = new Material(BaseLit) { color = color };
            return m;
        }
    }
}
