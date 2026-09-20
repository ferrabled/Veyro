using UnityEngine;
namespace MotionRunner.Art
{
    /// Author-time fitted sockets follow the animated bones. Dimensions are in normalized model units.
    public sealed class CharacterRig : MonoBehaviour
    {
        [System.Serializable]
        public struct HatFit
        {
            public Vector3 Size;
            public Vector3 Offset;
        }
        public Transform HatSocket,BackSocket;
        public float HeadWidth=0.32f,HeadDepth=0.32f;
        public HatFit Cap,TopHat,Crown;
        public HatFit Fit(string item) => item=="tophat" ? TopHat : item=="crown" ? Crown : Cap;
        public Renderer[] StockHeadwear;
        public Renderer[] StockBackwear;
        public void Dress(bool hat,bool back)
        {
            foreach(var r in StockHeadwear)if(r!=null)r.enabled=!hat;
            foreach(var r in StockBackwear)if(r!=null)r.enabled=!back;
        }
    }
}
