using UnityEngine;
namespace MotionRunner.Art
{
    /// A small idle flex; no skeletal simulation, extra particles or physics.
    public sealed class CosmeticWings : MonoBehaviour
    {
        void LateUpdate()
        {
            float flex=Mathf.Sin(Time.unscaledTime*1.8f)*5;
            if(transform.childCount!=2)return;
            transform.GetChild(0).localRotation=Quaternion.Euler(0,-18+flex,-12);
            transform.GetChild(1).localRotation=Quaternion.Euler(0,18-flex,12);
        }
    }
}
