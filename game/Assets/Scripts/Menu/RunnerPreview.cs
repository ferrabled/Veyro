using MotionRunner.Art;
using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MotionRunner.Menu
{
    /// One live model per visible page. Focus and rotation never change the equipped outfit.
    public sealed class RunnerPreview : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public const int Layer=30;
        GameObject _stage;
        RenderTexture _texture;
        RunnerVisual _visual;
        Transform _runner;
        Camera _camera;
        Vector3 _targetPosition;
        Quaternion _targetRotation;
        float _targetSize,_targetYaw=180,_yaw=180,_dragYaw,_angle,_burstAt;
        bool _dragging,_repeatBurst;
        CosmeticSlot _focus;
        static readonly System.Collections.Generic.HashSet<int> UsedStages=new System.Collections.Generic.HashSet<int>();
        int _stageId;
        public Camera PreviewCamera => _camera;
        public Transform Model => _runner;

        /// The preview's own runner, for callers that need a pose rather than an outfit - the
        /// result card asks for PlayResultPose here. Null before Build, and after the stage has
        /// been destroyed.
        public RunnerVisual Visual => _visual;
        public CosmeticSlot FocusedSlot => _focus;
        public static RunnerPreview Create(Transform parent)
        {
            var go=RuntimeUi.Element("RunnerPreview",parent,out var rect);
            RuntimeUi.Stretch(rect,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var preview=go.AddComponent<RunnerPreview>();preview.Build();return preview;
        }
        void Build()
        {
            _stage=new GameObject("Character preview stage");
            while(UsedStages.Contains(_stageId))_stageId++;UsedStages.Add(_stageId);
            // Reuse nearby stage positions to retain precision across repeated menu visits.
            _stage.transform.position=new Vector3(32+_stageId*6,0,0);
            var model=new GameObject("Preview runner");model.transform.SetParent(_stage.transform,false);
            model.transform.localPosition=Vector3.up*0.5f;_runner=model.transform;
            _runner.localRotation=Quaternion.Euler(0,180,0);
            _visual=RunnerVisual.Create(_runner);
            RunnerCosmetics.SetLayerRecursively(model,Layer);
            var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(_stage.transform,false);
            _camera=cameraObject.AddComponent<Camera>();_camera.cullingMask=1<<Layer;
            _camera.clearFlags=CameraClearFlags.SolidColor;_camera.backgroundColor=Color.clear;
            _camera.orthographic=true;_camera.nearClipPlane=0.1f;_camera.farClipPlane=10;
            _camera.allowHDR=false;_camera.allowMSAA=false;
            _texture=new RenderTexture(768,768,24) { name="Runner preview",antiAliasing=1 };
            _camera.targetTexture=_texture;
            var image=gameObject.AddComponent<RawImage>();image.texture=_texture;
            UpdateAspect();Focus(CosmeticSlot.Skin,true);
            _stage.SetActive(gameObject.activeInHierarchy);
        }
        public void Show(CosmeticLoadout loadout)
        {
            _visual?.ApplyLoadout(loadout);
            foreach(var system in _runner.GetComponentsInChildren<ParticleSystem>())
            { var main=system.main;main.useUnscaledTime=true; }
            Focus(_focus);
        }
        public void Focus(CosmeticSlot slot,bool immediate=false)
        {
            if(_camera==null)return;
            UpdateAspect();
            if(_focus!=slot)_dragYaw=0;
            _focus=slot;_repeatBurst=slot==CosmeticSlot.CrashFx;_fitFallen=false;
            _targetYaw=slot==CosmeticSlot.Back ? 20 : slot==CosmeticSlot.Trail ? 140:180;
            var target=new Vector3(0,0.82f,0);
            float pitch=4;_targetSize=0.94f;
            if(slot==CosmeticSlot.Headwear) { target.y=1.28f;_targetSize=0.54f;pitch=0; }
            else if(slot==CosmeticSlot.Body) { target.y=0.89f;_targetSize=0.64f;pitch=0; }
            else if(slot==CosmeticSlot.Trail) { target=new Vector3(0,0.62f,-0.15f);_targetSize=1.02f;pitch=12; }
            else if(slot==CosmeticSlot.Back) { target.y=0.88f;_targetSize=1.12f;pitch=4; }
            else if(slot==CosmeticSlot.Aura) { target.y=0.91f;_targetSize=1.12f;pitch=8; }
            else if(slot==CosmeticSlot.CrashFx) { target.y=0.86f;_targetSize=1.38f;pitch=8; }
            if(slot==CosmeticSlot.Skin || slot==CosmeticSlot.Back)
            {
                float top=1.45f,width=0.7f;
                foreach(var r in _runner.GetComponentsInChildren<Renderer>())
                {
                    if(!r.enabled || r is ParticleSystemRenderer || r.name=="Runner ground shadow")continue;
                    top=Mathf.Max(top,r.bounds.max.y-_stage.transform.position.y);
                    width=Mathf.Max(width,r.bounds.size.x);
                }
                target.y=top*0.51f;
                _targetSize=Mathf.Max(_targetSize,top*0.57f,width*0.57f/Mathf.Max(0.55f,_camera.aspect));
            }
            if(slot==CosmeticSlot.Headwear)
            {
                var rig=_runner.GetComponentInChildren<CharacterRig>();
                if(rig!=null)
                {
                    target.y=rig.HatSocket.position.y-_stage.transform.position.y;
                    float width=rig.HeadWidth*RunnerVisual.PresentationScale;
                    float top=target.y+width*0.55f;
                    foreach(var r in _runner.GetComponentsInChildren<MeshRenderer>())if(r.name.StartsWith("Cosmetic "))
                    {
                        // The bill sits ahead of the head. Fit its sweep around the rotation axis,
                        // not only the front-facing width, so dragging never clips it at the edge.
                        float radiusSquared=0;
                        foreach(var vertex in r.GetComponent<MeshFilter>().sharedMesh.vertices)
                        {
                            var offset=r.transform.TransformPoint(vertex)-_runner.position;
                            radiusSquared=Mathf.Max(radiusSquared,offset.x*offset.x+offset.z*offset.z);
                        }
                        width=Mathf.Max(width,2*Mathf.Sqrt(radiusSquared));
                        top=Mathf.Max(top,r.bounds.max.y-_stage.transform.position.y);
                    }
                    target.y=(target.y-rig.HeadWidth*RunnerVisual.PresentationScale*0.45f+top)*0.5f;
                    _targetSize=Mathf.Max(0.50f,width*0.55f/Mathf.Max(0.55f,_camera.aspect));
                }
            }
            _targetRotation=Quaternion.Euler(pitch,0,0);
            _targetPosition=target-_targetRotation*Vector3.forward*4;
            if(immediate)
            {
                _camera.transform.localPosition=_targetPosition;_camera.transform.localRotation=_targetRotation;
                _camera.orthographicSize=_targetSize;_yaw=_targetYaw;
                _runner.localRotation=Quaternion.Euler(0,_yaw+_dragYaw,0);
            }
            if(_repeatBurst)Burst();
        }
        public void Burst() { _visual?.PlayCrashEffect();_burstAt=Time.unscaledTime+2.6f; }

        /// Framing for a character that is about to end up on the floor (the result card's
        /// defeat pose): seen from the side and a little from above, so the whole body reads as
        /// lying down instead of a head looming at the bottom of a standing-height frame.
        public void FrameFallen()
        {
            if(_camera==null)return;
            UpdateAspect();
            _focus=CosmeticSlot.Skin;_repeatBurst=false;_dragYaw=0;_fitFallen=true;
            _targetYaw=_yaw=100;
            _targetRotation=Quaternion.Euler(28,0,0);
            _runner.localRotation=Quaternion.Euler(0,_yaw,0);
            FitFallen();
            _camera.transform.localPosition=_targetPosition;_camera.transform.localRotation=_targetRotation;
            _camera.orthographicSize=_targetSize;
        }

        /// Frames whatever the body is doing right now, from its bones: a fall carries the hips
        /// a body length away from the root, and the skinned renderers' bounds stay at the
        /// bind pose, so neither the root nor renderer.bounds can be trusted once the character
        /// is on the floor. Re-run every frame while the fallen framing is active, so the camera
        /// follows the fall and settles on the body wherever it ends up.
        void FitFallen()
        {
            var animator=_runner.GetComponentInChildren<Animator>();
            if(animator==null || !animator.isHuman)return;
            bool first=true;var bounds=new Bounds();
            foreach(var bone in FramingBones)
            {
                var t=animator.GetBoneTransform(bone);if(t==null)continue;
                var p=t.position-_stage.transform.position;
                if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
            }
            if(first)return;
            foreach(var r in _runner.GetComponentsInChildren<MeshRenderer>())
                if(r.name.StartsWith("Cosmetic "))bounds.Encapsulate(new Bounds(r.bounds.center-_stage.transform.position,r.bounds.size));
            bounds.Expand(0.34f); // body thickness around the bone line, plus a little air
            // Orthographic fit: the box's half-extents as seen from the camera's own axes.
            var inverse=Quaternion.Inverse(_targetRotation);float halfX=0,halfY=0;
            for(int i=0;i<8;i++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                var local=inverse*(corner-bounds.center);
                halfX=Mathf.Max(halfX,Mathf.Abs(local.x));halfY=Mathf.Max(halfY,Mathf.Abs(local.y));
            }
            float aspect=Mathf.Max(0.55f,_camera.aspect);
            _targetSize=Mathf.Max(0.55f,halfY*1.12f,halfX*1.12f/aspect);
            _targetPosition=bounds.center-_targetRotation*Vector3.forward*4;
        }
        static readonly HumanBodyBones[] FramingBones=
        {
            HumanBodyBones.Hips,HumanBodyBones.Head,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,
            HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftLowerLeg,HumanBodyBones.RightLowerLeg,
            HumanBodyBones.Chest
        };
        bool _fitFallen;
        void UpdateAspect()
        {
            if(_camera==null)return;
            var rect=((RectTransform)transform).rect;
            _camera.aspect=rect.height>1 && rect.width>1 ? rect.width/rect.height:1;
        }
        void LateUpdate()
        {
            float aspect=_camera.aspect;UpdateAspect();
            if(Mathf.Abs(aspect-_camera.aspect)>0.01f && !_fitFallen)Focus(_focus);
            if(_fitFallen)FitFallen();
            float blend=1-Mathf.Exp(-9*Time.unscaledDeltaTime);
            _camera.transform.localPosition=Vector3.Lerp(_camera.transform.localPosition,_targetPosition,blend);
            _camera.transform.localRotation=Quaternion.Slerp(_camera.transform.localRotation,_targetRotation,blend);
            _camera.orthographicSize=Mathf.Lerp(_camera.orthographicSize,_targetSize,blend);
            _yaw=Mathf.LerpAngle(_yaw,_targetYaw,blend);
            _angle+=Time.unscaledDeltaTime*0.7f;
            _runner.localRotation=Quaternion.Euler(0,_yaw+_dragYaw+(_dragging ? 0:Mathf.Sin(_angle)*7),0);
            if(_repeatBurst && Time.unscaledTime>=_burstAt)Burst();
        }
        public void OnBeginDrag(PointerEventData data) => _dragging=true;
        public void OnDrag(PointerEventData data) => _dragYaw-=data.delta.x*0.3f;
        public void OnEndDrag(PointerEventData data) => _dragging=false;
        void OnEnable() { if(_stage!=null) { _stage.SetActive(true);_visual?.ResetPose();UpdateAspect(); } }
        void OnDisable() { if(_stage!=null)_stage.SetActive(false);_dragging=false; }
        void OnDestroy()
        {
            UsedStages.Remove(_stageId);
            if(_texture!=null)_texture.Release();
            if(Application.isPlaying) { Destroy(_stage);Destroy(_texture); }
            else { DestroyImmediate(_stage);DestroyImmediate(_texture); }
        }
    }
}
