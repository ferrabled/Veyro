using MotionRunner.Gameplay;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Art
{
    /// Animation is presentation only: the controller owns the root and collision bounds.
    public sealed class RunnerVisual : MonoBehaviour
    {
        Animator _animation;
        Transform _model;
        Transform _shadow;
        Material _outfit;
        string _playing;
        float _previousX;

        public static RunnerVisual Create(Transform parent)
        {
            var assets=ParkAssets.Load();
            var visual=parent.gameObject.AddComponent<RunnerVisual>();
            var model=Instantiate(assets.Runner,parent,false);
            visual._model=model.transform;
            visual._outfit=new Material(assets.RunnerMaterial);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=visual._outfit;
            visual._animation=model.GetComponentInChildren<Animator>();
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name="Runner ground shadow";
            shadow.transform.SetParent(parent,false);
            shadow.transform.localScale=new Vector3(0.64f,0.003f,0.44f);
            Destroy(shadow.GetComponent<Collider>());
            shadow.GetComponent<Renderer>().sharedMaterial=Core.RuntimeMaterials.Shared(ParkTheme.Hex(0x9DAB97));
            visual._shadow=shadow.transform;
            visual.ResetPose();
            return visual;
        }

        public void SetOutfit(Color color) => _outfit.SetColor("_OutfitColor",color);

        public void ResetPose()
        {
            _previousX=transform.position.x;
            if(_model!=null) _model.localRotation=Quaternion.identity;
            Play("idle");
            UpdateShadow();
        }

        public void Step(float deltaTime, bool airborne)
        {
            if(deltaTime<=0) return;
            float velocity=(transform.position.x-_previousX)/deltaTime;
            _previousX=transform.position.x;
            _model.localRotation=Quaternion.Slerp(_model.localRotation,
                Quaternion.Euler(0,Mathf.Clamp(velocity*1.2f,-12,12),Mathf.Clamp(-velocity*0.9f,-10,10)),
                1-Mathf.Exp(-16*deltaTime));
            Play(airborne?"jump":"run");
            UpdateShadow();
        }

        void UpdateShadow()
        {
            if(_shadow==null) return;
            _shadow.position=new Vector3(transform.position.x,0.012f,transform.position.z);
            float scale=Mathf.Lerp(1,0.6f,Mathf.Clamp01(transform.position.y-TrackMetrics.RunnerRestY));
            _shadow.localScale=new Vector3(0.64f*scale,0.003f,0.44f*scale);
        }

        void Play(string clip)
        {
            if(_animation==null || _playing==clip) return;
            _playing=clip;
            _animation.CrossFadeInFixedTime(clip,0.10f);
        }

        void OnDestroy() { if(_outfit!=null) Destroy(_outfit); }
    }
}
