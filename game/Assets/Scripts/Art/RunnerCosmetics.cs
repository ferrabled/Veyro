using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionRunner.Art
{
    /// Visual-only effects. One instance per runner/preview; nothing touches gameplay RNG or bounds.
    public sealed class RunnerCosmetics : MonoBehaviour
    {
        GameObject _hat,_back;
        CosmeticTrail _trail;
        ParticleSystem _aura, _crash;
        Material _auraMaterial, _crashMaterial;
        string _appearance;
        public bool HasCrashEffect => _crash!=null;
        public int LiveParticleLimit => (_aura!=null ? 24:0)+(_crash!=null ? 30:0)+(_trail!=null ? 36:0);
        public void Apply(CosmeticLoadout loadout, Animator animator, Material outfit, CharacterRig rig)
        {
            string appearance=loadout.Serialize();
            if(_appearance==appearance) return;
            _appearance=appearance;
            Clear();
            var item=loadout.Outfit;
            outfit.SetColor("_OutfitColor",SkinService.ToColor(item.BodyColor));
            outfit.SetFloat("_Metallic",item.Id=="chrome" || item.Id=="prism" ? 1 : item.Id=="gold" ? 0.35f : 0);
            outfit.SetFloat("_Glow",item.Id=="neon" || item.Id=="ember" || item.Id=="frost" ? 0.25f : 0);
            outfit.SetFloat("_Prism",item.Id=="prism" ? 1 : 0);
            var art=SeasonArt.Load();
            if(art==null) return;
            var prefab=art.Hat(loadout.Headwear);
            rig?.Dress(prefab!=null,!string.IsNullOrEmpty(loadout.Back));
            if(prefab!=null && rig!=null && rig.HatSocket!=null)
            {
                _hat=Instantiate(prefab,rig.HatSocket,false);_hat.name="Cosmetic "+loadout.Headwear;
                // Hat meshes are authored around their opening, not around the brim's bounding box.
                var scale=rig.HatSocket.lossyScale;
                var fit=rig.Fit(loadout.Headwear);
                _hat.transform.localScale=new Vector3(fit.Size.x*RunnerVisual.PresentationScale/Mathf.Abs(scale.x),
                    fit.Size.y*RunnerVisual.PresentationScale/Mathf.Abs(scale.y),fit.Size.z*RunnerVisual.PresentationScale/Mathf.Abs(scale.z));
                _hat.transform.localPosition=rig.HatSocket.InverseTransformVector(rig.transform.TransformVector(fit.Offset));
            }
            var attachment=CharacterArt.Load()?.Attachment(loadout.Back);
            if(attachment!=null && rig!=null && rig.BackSocket!=null)
            {
                _back=Instantiate(attachment,rig.BackSocket,false);_back.name="Cosmetic "+loadout.Back;
                var scale=rig.BackSocket.lossyScale;
                _back.transform.localScale=Vector3.Scale(attachment.transform.localScale,new Vector3(RunnerVisual.PresentationScale/Mathf.Abs(scale.x),
                    RunnerVisual.PresentationScale/Mathf.Abs(scale.y),RunnerVisual.PresentationScale/Mathf.Abs(scale.z)));
            }
            if(!string.IsNullOrEmpty(loadout.Trail))
            {
                var go=new GameObject("Cosmetic foot sparks");go.transform.SetParent(transform,false);
                _trail=go.AddComponent<CosmeticTrail>();_trail.Configure(loadout.Trail,art);
            }
            if(!string.IsNullOrEmpty(loadout.Aura))
            {
                bool comet=loadout.Aura=="comet";
                _auraMaterial=new Material(art.Effects);
                _auraMaterial.SetTexture("_BaseMap",comet ? art.Star : art.Spark);
                _aura=Particles("Cosmetic aura",_auraMaterial,24,true);
                var main=_aura.main; main.startLifetime=1.1f; main.startSpeed=0.15f;
                main.startSize=new ParticleSystem.MinMaxCurve(0.045f,comet ? 0.11f:0.08f);
                Color c=loadout.Aura=="ember" ? ParkTheme.Hex(0xFFAD45) : loadout.Aura=="frost" ? ParkTheme.Hex(0xBAF1FA) :
                    comet ? ParkTheme.Hex(0xADA6F1) : ParkTheme.Hex(0xFADB70);
                main.startColor=c;
                var shape=_aura.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=0.5f;
                var velocity=_aura.velocityOverLifetime; velocity.enabled=true;
                velocity.space=ParticleSystemSimulationSpace.Local; velocity.z=-0.6f; velocity.y=0.25f;
                _aura.transform.localPosition=Vector3.up*0.5f;
                _aura.Play();
            }
            if(loadout.CrashFx=="confetti")
            {
                _crashMaterial=new Material(art.Effects); _crashMaterial.SetTexture("_BaseMap",Texture2D.whiteTexture);
                _crash=Particles("Cosmetic crash confetti",_crashMaterial,30,false);
                var main=_crash.main; main.startLifetime=0.85f;
                main.startSpeed=new ParticleSystem.MinMaxCurve(1.2f,2.6f);
                main.startSize=new ParticleSystem.MinMaxCurve(0.035f,0.07f); main.gravityModifier=0.35f;
                main.startColor=new ParticleSystem.MinMaxGradient(ParkTheme.Pink,ParkTheme.Hex(0xF4D660));
                main.startRotation=new ParticleSystem.MinMaxCurve(0,6.28f);
                var shape=_crash.shape; shape.shapeType=ParticleSystemShapeType.Sphere; shape.radius=0.15f;
                _crash.transform.localPosition=Vector3.up*0.2f;
            }
            SetLayerRecursively(gameObject,gameObject.layer);
        }
        ParticleSystem Particles(string name,Material material,int max,bool loop)
        {
            var go=new GameObject(name); go.transform.SetParent(transform,false);
            var system=go.AddComponent<ParticleSystem>(); system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=system.main; main.loop=loop; main.playOnAwake=false; main.maxParticles=max;
            main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=system.emission; emission.enabled=loop; emission.rateOverTime=12;
            var alpha=system.colorOverLifetime; alpha.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(0.85f,0.15f),new GradientAlphaKey(0,1)});
            alpha.color=gradient;
            var renderer=system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return system;
        }
        public void Crash() { if(_crash!=null) { _crash.Play(); _crash.Emit(28); } }
        public void ResetEffects() { _trail?.ResetTrail(); _crash?.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); if(_aura!=null) { _aura.Clear();_aura.Play(); } }
        public void Release(){_appearance=null;Clear();}
        void Clear()
        {
            Dispose(_hat);Dispose(_back);if(_trail!=null)Dispose(_trail.gameObject);
            if(_aura!=null) Dispose(_aura.gameObject); if(_crash!=null) Dispose(_crash.gameObject);
            Dispose(_auraMaterial); Dispose(_crashMaterial);
            _hat=null;_back=null;_trail=null; _aura=null; _crash=null;
        }
        public static void SetLayerRecursively(GameObject root,int layer)
        {
            root.layer=layer;
            foreach(Transform child in root.transform) SetLayerRecursively(child.gameObject,layer);
        }
        static void Dispose(Object obj) { if(obj==null)return;if(obj is GameObject go)go.SetActive(false); if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void OnDestroy() => Clear();
    }

    /// Short, sparse foot sparks express forward travel in a scrolling world without a floating ribbon.
    public sealed class CosmeticTrail : MonoBehaviour
    {
        ParticleSystem _particles;
        Material _material;
        public void Configure(string id,SeasonArt art)
        {
            transform.localPosition=new Vector3(0,-0.44f,-0.12f);
            _material=new Material(art.Effects);_material.SetTexture("_BaseMap",id=="aurora" || id=="frost" ? art.Star : art.Spark);
            _particles=gameObject.AddComponent<ParticleSystem>();_particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=_particles.main;main.loop=true;main.playOnAwake=false;main.maxParticles=36;
            main.simulationSpace=ParticleSystemSimulationSpace.Local;main.startLifetime=new ParticleSystem.MinMaxCurve(0.35f,0.65f);
            main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(id=="shadow" ? 0.045f:0.025f,id=="shadow" ? 0.13f:0.075f);
            var item=CosmeticCatalog.Find(id);
            Color color=item!=null ? SkinService.ToColor(item.BodyColor) : id=="ember" ? ParkTheme.Hex(0xFFA759) : ParkTheme.Hex(0xC3F1FF);
            color.a=id=="shadow" ? 0.30f:0.75f;
            main.startColor=new ParticleSystem.MinMaxGradient(color,id=="aurora" || id=="twin" ? ParkTheme.Pink:color);
            var emission=_particles.emission;emission.rateOverTime=28;
            var shape=_particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(0.28f,0.03f,0.06f);
            var velocity=_particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;
            velocity.z=-1.6f;velocity.y=0.12f;
            var size=_particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            var colorLife=_particles.colorOverLifetime;colorLife.enabled=true;
            var fade=new Gradient();fade.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(0.85f,0.12f),new GradientAlphaKey(0,1)});colorLife.color=fade;
            var renderer=_particles.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            _particles.Play();
        }
        public void ResetTrail(){if(_particles!=null){_particles.Clear();_particles.Play();}}
        void OnDestroy(){if(Application.isPlaying)Destroy(_material);else DestroyImmediate(_material);}
    }
}
