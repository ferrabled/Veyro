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
        string _character;
        CharacterRig _rig;
        RunnerCosmetics _cosmetics;
        string _playing;
        float _previousX;
        float _dodgeRemaining;
        int _moveDirection;
        string _dodge;
        bool _crashed;

        public static RunnerVisual Create(Transform parent)
        {
            var visual=parent.gameObject.AddComponent<RunnerVisual>();
            visual.SetCharacter("runner");
            // A transparent feathered contact shadow has no raised geometry or opaque platform.
            var shadow=GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name="Runner ground shadow";shadow.transform.SetParent(parent,false);
            shadow.transform.localRotation=Quaternion.Euler(90,0,0);
            if(Application.isPlaying) Destroy(shadow.GetComponent<Collider>());else DestroyImmediate(shadow.GetComponent<Collider>());
            shadow.GetComponent<Renderer>().sharedMaterial=SeasonArt.Load().Shadow;
            visual._shadow=shadow.transform;
            visual.ResetPose();
            return visual;
        }

        void SetCharacter(string id)
        {
            string character=id=="ember" || id=="frost" ? id : "runner";
            if(_character==character)return;
            _character=character;
            if(_cosmetics!=null)_cosmetics.Release();
            if(_model!=null){_model.gameObject.SetActive(false);Dispose(_model.gameObject);}
            Dispose(_outfit);
            var art=CharacterArt.Load();
            var model=Instantiate(art!=null ? art.Character(character) : ParkAssets.Load().Runner,transform,false);
            _model=model.transform;_model.localScale=Vector3.one*PresentationScale;
            _model.localPosition=Vector3.up*(PresentationScale-1)*TrackMetrics.RunnerRestY;
            _outfit=new Material(art!=null ? art.Outfit(character) : ParkAssets.Load().RunnerMaterial);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=_outfit;
            _animation=model.GetComponentInChildren<Animator>();_rig=model.GetComponent<CharacterRig>();
            _playing=null;_animation.Rebind();_animation.Play("idle",0,0);_animation.Update(0);
            RunnerCosmetics.SetLayerRecursively(model,gameObject.layer);
            ResetPose();
        }
        static void Dispose(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        public void SetOutfit(Color color) => _outfit.SetColor("_OutfitColor",color);

        public void ApplyLoadout(CosmeticLoadout loadout)
        {
            SetCharacter(loadout.Skin);
            if(_cosmetics==null) _cosmetics=gameObject.AddComponent<RunnerCosmetics>();
            _cosmetics.Apply(loadout,_animation,_outfit,_rig);
        }
        public bool HasCrashEffect => _cosmetics!=null && _cosmetics.HasCrashEffect;
        /// The run just ended against an obstacle: the crash cosmetic fires and the runner takes
        /// the crash pose - stopped dead against a wall, or tripping over a hurdle. The clips do
        /// not loop and hold their last frame, and Step is ignored until ResetPose, so nothing
        /// can slide the runner back into the run cycle while the card is up.
        public void Crash(CrashKind crash)
        {
            PlayCrashEffect();
            _crashed=true;
            _dodgeRemaining=0;
            if(_model!=null) _model.localRotation=Quaternion.identity;
            Play(crash==CrashKind.WallSlam ? "crashWall" : "crashTrip", 0.05f);
        }

        /// How long the world crash pose needs on screen before the result card may cover it.
        public const float CrashPoseSeconds = 1.0f;

        /// The result card's character pose: a celebration when the run was a new record, a
        /// defeat (falls to the ground) otherwise. Loops or holds its last frame until ResetPose.
        public void PlayResultPose(bool celebrate)
        {
            _crashed=true;
            if(_model!=null) _model.localRotation=Quaternion.identity;
            _playing=null; // A fresh pose every time the card opens, even twice in a row.
            Play(celebrate ? "celebrate" : "defeat", 0.15f);
        }

        /// Only the crash cosmetic (confetti etc.), no pose change - the locker preview's replay.
        public void PlayCrashEffect() => _cosmetics?.Crash();
        public void ResetPose()
        {
            _cosmetics?.ResetEffects();
            _crashed=false;
            _previousX=transform.position.x;
            _dodgeRemaining=0;
            _moveDirection=0;
            if(_model!=null) _model.localRotation=Quaternion.identity;
            _playing=null;
            Play("idle");
            UpdateShadow();
        }

        public void Step(float deltaTime, bool airborne)
        {
            if(deltaTime<=0 || _crashed) return;
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
            _shadow.localScale=new Vector3(0.60f*scale*PresentationScale,0.42f*scale*PresentationScale,1);
        }

        void Play(string clip, float fade=-1)
        {
            if(_animation==null || _playing==clip) return;
            // A controller built before the crash/result clips were imported has no such state;
            // the runner then simply keeps its current pose rather than logging every frame.
            if(!_animation.HasState(0,Animator.StringToHash(clip))) return;
            _playing=clip;
            if(fade<0) fade=clip.StartsWith("dodge") ? 0.035f : 0.10f;
            _animation.CrossFadeInFixedTime(clip,fade,0,0);
        }

        void OnDestroy()
        {
            if(_outfit==null) return;
            if(Application.isPlaying) Destroy(_outfit); else DestroyImmediate(_outfit);
        }
    }
}
