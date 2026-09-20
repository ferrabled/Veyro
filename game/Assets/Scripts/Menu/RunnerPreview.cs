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
        static int _nextStage;
        public Camera PreviewCamera => _camera;
        public Transform Model => _runner;
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
            _stage.transform.position=new Vector3(3000+(_nextStage++%1000)*12,0,0);
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
            _texture=new RenderTexture(768,768,16) { name="Runner preview",antiAliasing=1 };
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
        }
        public void Focus(CosmeticSlot slot,bool immediate=false)
        {
            if(_camera==null)return;
            if(_focus!=slot)_dragYaw=0;
            _focus=slot;_repeatBurst=slot==CosmeticSlot.CrashFx;
            _targetYaw=slot==CosmeticSlot.Trail ? 35:180;
            var target=new Vector3(0,0.82f,0);
            float pitch=4;_targetSize=0.94f;
            if(slot==CosmeticSlot.Headwear) { target.y=1.28f;_targetSize=0.54f;pitch=0; }
            else if(slot==CosmeticSlot.Body) { target.y=0.89f;_targetSize=0.64f;pitch=0; }
            else if(slot==CosmeticSlot.Trail) { target=new Vector3(-0.35f,0.45f,-0.65f);_targetSize=1.38f;pitch=23; }
            else if(slot==CosmeticSlot.Aura) { target.y=0.91f;_targetSize=1.12f;pitch=8; }
            else if(slot==CosmeticSlot.CrashFx) { target.y=0.86f;_targetSize=1.38f;pitch=8; }
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
        public void Burst() { _visual?.Crash();_burstAt=Time.unscaledTime+2.6f; }
        void UpdateAspect()
        {
            if(_camera==null)return;
            var rect=((RectTransform)transform).rect;
            _camera.aspect=rect.height>1 && rect.width>1 ? rect.width/rect.height:1;
        }
        void LateUpdate()
        {
            UpdateAspect();
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
            if(_texture!=null)_texture.Release();
            if(Application.isPlaying) { Destroy(_stage);Destroy(_texture); }
            else { DestroyImmediate(_stage);DestroyImmediate(_texture); }
        }
    }
}
