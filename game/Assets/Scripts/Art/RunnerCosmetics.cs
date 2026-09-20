using MotionRunner.Commerce;
using MotionRunner.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionRunner.Art
{
    /// Visual-only effects. One instance per runner/preview; nothing touches gameplay RNG or bounds.
    public sealed class RunnerCosmetics : MonoBehaviour
    {
        GameObject _hat;
        CosmeticRibbon _ribbon;
        ParticleSystem _aura, _crash;
        Material _auraMaterial, _crashMaterial;
        string _appearance;
        public bool HasCrashEffect => _crash!=null;
        public int LiveParticleLimit => (_aura!=null ? 24:0)+(_crash!=null ? 30:0);
        public void Apply(CosmeticLoadout loadout, Animator animator, Material outfit)
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
            if(prefab!=null && animator!=null)
            {
                var head=animator.GetBoneTransform(HumanBodyBones.Head);
                _hat=Instantiate(prefab,head,false);
                _hat.name="Cosmetic "+loadout.Headwear;
                // The imported Kenney skeleton carries an FBX unit scale. Normalize against the
                // head's world scale instead of inheriting that scale a second time.
                var boneScale=head.lossyScale;
                _hat.transform.localScale=new Vector3(RunnerVisual.PresentationScale/Mathf.Abs(boneScale.x),
                    RunnerVisual.PresentationScale/Mathf.Abs(boneScale.y),RunnerVisual.PresentationScale/Mathf.Abs(boneScale.z));
                _hat.transform.position=head.position+head.up*0.34f;
                _hat.transform.localRotation=Quaternion.Euler(0,loadout.Headwear=="cap" ? -90:0,0);
            }
            if(!string.IsNullOrEmpty(loadout.Trail))
            {
                var go=new GameObject("Cosmetic ribbon"); go.transform.SetParent(transform,false);
                _ribbon=go.AddComponent<CosmeticRibbon>(); _ribbon.Configure(loadout.Trail,art.Effects,art.Trace);
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
        public void ResetEffects() { _ribbon?.ResetTrail(); _crash?.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear); if(_aura!=null) { _aura.Clear();_aura.Play(); } }
        void Clear()
        {
            Dispose(_hat); if(_ribbon!=null) Dispose(_ribbon.gameObject);
            if(_aura!=null) Dispose(_aura.gameObject); if(_crash!=null) Dispose(_crash.gameObject);
            Dispose(_auraMaterial); Dispose(_crashMaterial);
            _hat=null; _ribbon=null; _aura=null; _crash=null;
        }
        public static void SetLayerRecursively(GameObject root,int layer)
        {
            root.layer=layer;
            foreach(Transform child in root.transform) SetLayerRecursively(child.gameObject,layer);
        }
        static void Dispose(Object obj) { if(obj==null)return; if(Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void OnDestroy() => Clear();
    }

    /// A bounded ribbon for the scrolling-world runner. History is sampled at fixed time intervals,
    /// so 30/60/90 FPS renderers have the same trail length. Never allocates in Update.
    public sealed class CosmeticRibbon : MonoBehaviour
    {
        const int Points=16;
        readonly Vector3[] _history=new Vector3[Points];
        Vector3[] _vertices;
        Color[] _colors;
        Mesh _mesh;
        Material _material;
        string _id;
        int _ribbons;
        float _sample;
        public void Configure(string id,Material source,Texture texture)
        {
            _id=id; _ribbons=id=="twin" ? 2:1;
            _vertices=new Vector3[Points*2*_ribbons]; _colors=new Color[_vertices.Length];
            var uv=new Vector2[_vertices.Length]; var indices=new int[(Points-1)*6*_ribbons];
            for(int r=0;r<_ribbons;r++) for(int i=0;i<Points;i++)
            {
                int v=(r*Points+i)*2; uv[v]=new Vector2(0,i/(float)(Points-1)); uv[v+1]=new Vector2(1,i/(float)(Points-1));
                if(i==Points-1)continue;
                int t=(r*(Points-1)+i)*6;
                indices[t]=v;indices[t+1]=v+2;indices[t+2]=v+1;indices[t+3]=v+1;indices[t+4]=v+2;indices[t+5]=v+3;
            }
            _mesh=new Mesh { name="Cosmetic ribbon" }; _mesh.MarkDynamic();
            _mesh.vertices=_vertices;_mesh.uv=uv;_mesh.triangles=indices;
            gameObject.AddComponent<MeshFilter>().sharedMesh=_mesh;
            _material=new Material(source);_material.SetTexture("_BaseMap",texture);_material.SetFloat("_Ribbon",1);
            var renderer=gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ResetTrail();
        }
        public void ResetTrail() { for(int i=0;i<Points;i++) _history[i]=transform.position; _sample=0; }
        void LateUpdate() => StepVisual(Time.deltaTime,Time.time);
        public void StepVisual(float deltaTime,float elapsedTime)
        {
            if(_mesh==null || deltaTime<=0)return;
            _sample+=deltaTime;
            if(_sample>=1f/30f)
            {
                _sample%=1f/30f;
                for(int i=Points-1;i>0;i--)_history[i]=_history[i-1];
                _history[0]=transform.position;
            }
            var item=CosmeticCatalog.Find(_id); Color color=item==null ? (_id=="ember" ? new Color(1,0.4f,0.1f) : new Color(0.2f,0.72f,0.95f)) : SkinService.ToColor(item.BodyColor);
            for(int r=0;r<_ribbons;r++)for(int i=0;i<Points;i++)
            {
                float age=i/(float)(Points-1);
                var p=transform.InverseTransformPoint(_history[i]);
                p+=new Vector3(_ribbons==2 ? (r==0 ? -0.22f:0.22f) : 0,-0.26f,-0.2f-age*2.4f);
                float width=(1-age)*0.18f;
                int v=(r*Points+i)*2;_vertices[v]=p+Vector3.left*width;_vertices[v+1]=p+Vector3.right*width;
                Color tint=_id=="aurora" ? Color.Lerp(ParkTheme.Mint,ParkTheme.Pink,0.5f+0.5f*Mathf.Sin(elapsedTime*2-age*5)) : color;
                tint.a=(1-age)*(_id=="cyan" ? 0.6f+0.25f*Mathf.Sin(elapsedTime*5) : 0.8f);
                _colors[v]=_colors[v+1]=tint;
            }
            _mesh.vertices=_vertices;_mesh.colors=_colors;_mesh.RecalculateBounds();
        }
        void OnDestroy()
        {
            if(Application.isPlaying) { Destroy(_mesh);Destroy(_material); }
            else { DestroyImmediate(_mesh);DestroyImmediate(_material); }
        }
    }
}
