using MotionRunner.Gameplay;
using MotionRunner.Commerce;
using MotionRunner.Track;
using UnityEngine;

namespace MotionRunner.Art
{
    /// Animation is presentation only: the controller owns the root and collision bounds.
    public sealed class RunnerVisual : MonoBehaviour
    {
        public const float PresentationScale = 1.45f;
        public const float DodgeDuration = 0.22f;
        Animator _animation;
        Transform _model;
        Transform _shadow;
        Material _outfit;
        RunnerCosmetics _cosmetics;
        string _playing;
        float _previousX;
        float _dodgeRemaining;
        int _moveDirection;
        string _dodge;

        public static RunnerVisual Create(Transform parent)
        {
            var assets=ParkAssets.Load();
            var visual=parent.gameObject.AddComponent<RunnerVisual>();
            var model=Instantiate(assets.Runner,parent,false);
            visual._model=model.transform;
            // Scale about the feet, while leaving the controller's established AABB intact.
            model.transform.localScale=Vector3.one*PresentationScale;
            model.transform.localPosition=Vector3.up*(PresentationScale-1f)*TrackMetrics.RunnerRestY;
            visual._outfit=new Material(assets.RunnerMaterial);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial=visual._outfit;
            visual._animation=model.GetComponentInChildren<Animator>();
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shadow.name="Runner ground shadow";
            shadow.transform.SetParent(parent,false);
            shadow.transform.localScale=new Vector3(0.64f,0.003f,0.44f);
            if(Application.isPlaying) Destroy(shadow.GetComponent<Collider>());
            else DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.GetComponent<Renderer>().sharedMaterial=Core.RuntimeMaterials.Shared(ParkTheme.Hex(0x9DAB97));
            visual._shadow=shadow.transform;
            visual.ResetPose();
            return visual;
        }

        public void SetOutfit(Color color) => _outfit.SetColor("_OutfitColor",color);

        public void ApplyLoadout(CosmeticLoadout loadout)
        {
            if(_cosmetics==null) _cosmetics=gameObject.AddComponent<RunnerCosmetics>();
            _cosmetics.Apply(loadout,_animation,_outfit);
        }
        public bool HasCrashEffect => _cosmetics!=null && _cosmetics.HasCrashEffect;
        public void Crash() => _cosmetics?.Crash();
        public void ResetPose()
        {
            _cosmetics?.ResetEffects();
            _previousX=transform.position.x;
            _dodgeRemaining=0;
            _moveDirection=0;
            if(_model!=null) _model.localRotation=Quaternion.identity;
            Play("idle");
            UpdateShadow();
        }

        public void Step(float deltaTime, bool airborne)
        {
            if(deltaTime<=0) return;
            float velocity=(transform.position.x-_previousX)/deltaTime;
            _previousX=transform.position.x;
            int direction=Mathf.Abs(velocity)>0.3f ? (velocity>0 ? 1 : -1) : 0;
            _dodgeRemaining=Mathf.Max(0,_dodgeRemaining-deltaTime);
            if(!airborne && direction!=0 && direction!=_moveDirection)
            {
                _dodge=direction<0 ? "dodgeLeft" : "dodgeRight";
                _dodgeRemaining=DodgeDuration;
                _playing=null; // A rapid reversal or repeated step starts a fresh pose.
            }
            _moveDirection=direction;
            if(airborne) _dodgeRemaining=0;
            _model.localRotation=Quaternion.Slerp(_model.localRotation,
                Quaternion.Euler(0,Mathf.Clamp(velocity*0.6f,-7,7),Mathf.Clamp(-velocity*0.4f,-5,5)),
                1-Mathf.Exp(-16*deltaTime));
            Play(airborne ? "jump" : _dodgeRemaining>0 ? _dodge : "run");
            UpdateShadow();
        }

        void UpdateShadow()
        {
            if(_shadow==null) return;
            _shadow.position=new Vector3(transform.position.x,0.012f,transform.position.z);
            float scale=Mathf.Lerp(1,0.6f,Mathf.Clamp01(transform.position.y-TrackMetrics.RunnerRestY));
            _shadow.localScale=new Vector3(0.64f*scale*PresentationScale,0.003f,0.44f*scale*PresentationScale);
        }

        void Play(string clip)
        {
            if(_animation==null || _playing==clip) return;
            _playing=clip;
            _animation.CrossFadeInFixedTime(clip,clip.StartsWith("dodge") ? 0.035f : 0.10f,0,0);
        }

        void OnDestroy()
        {
            if(_outfit==null) return;
            if(Application.isPlaying) Destroy(_outfit); else DestroyImmediate(_outfit);
        }
    }
}
