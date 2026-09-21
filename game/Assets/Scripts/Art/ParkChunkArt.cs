using System.Collections.Generic;
using MotionRunner.Core;
using MotionRunner.Track;
using UnityEngine;
using UnityEngine.Rendering;

namespace MotionRunner.Art
{
    /// Built once per pooled view. Scenery uses its own stable seed; gameplay never observes it.
    public sealed class ParkChunkArt : MonoBehaviour
    {
        struct Batch { public Mesh Mesh; public Material Material; }
        static readonly Dictionary<string, Batch[]> Cache = new Dictionary<string, Batch[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearCache()
        {
            foreach (var batches in Cache.Values)
                foreach (var batch in batches)
                    if (batch.Mesh != null) Destroy(batch.Mesh);
            Cache.Clear();
        }

        public static void Build(Transform parent, ChunkDefinition definition)
        {
            var go = new GameObject("Park scenery");
            go.transform.SetParent(parent, false);
            go.AddComponent<ParkChunkArt>().Create(definition);
        }

        void Create(ChunkDefinition d)
        {
            if (Cache.TryGetValue(d.ChunkId, out var cached))
            {
                foreach (var batch in cached) AddBatch(batch);
                return;
            }
            var assets = ParkAssets.Load();
            int variant = 0;
            foreach (var chunk in assets.Chunks)
                if (chunk.ChunkId == d.ChunkId) { variant = chunk.SceneryVariant; break; }
            var random = new RunSeed(variant + 1, "park-art-1", d.ChunkId).CreateRandom();
            bool bridge = d.HasTag(ChunkTag.Bridge);
            float length = d.Length;
            Box("Landscape", new Vector3(0,-0.65f,length/2), new Vector3(48,0.5f,length),
                bridge ? ParkTheme.Hex(0x6DBBB6) : ParkTheme.Hex(0x84B89D));
            for (int side=-1; side<=1; side+=2)
            {
                Box("Garden bank", new Vector3(side*14,-0.30f,length/2),
                    new Vector3(bridge ? 15 : 22,0.45f,length), ParkTheme.Mint);
                // Broad, quiet edging separates the playable corridor from decoration.
                Box("Path edge",new Vector3(side*2.55f,-0.07f,length/2),new Vector3(0.30f,0.22f,length),ParkTheme.Stone);
                if (bridge)
                {
                    Box("Bridge handrail",new Vector3(side*2.58f,0.65f,length/2),new Vector3(0.13f,0.12f,length),ParkTheme.Ink);
                    for(float z=1;z<length;z+=4)
                        Box("Bridge post",new Vector3(side*2.58f,0.33f,z),new Vector3(0.18f,0.66f,0.18f),ParkTheme.Ink);
                }
                for(int j=0;j<4;j++)
                {
                    float z = (j+0.4f)*(length/4);
                    float x = side*(bridge ? 8.7f : 5.2f + random.NextFloat()*1.4f);
                    Prop(assets.Props[(j+variant)%3], new Vector3(x,-0.08f,z),
                        2.8f+random.NextFloat()*2.0f, random.NextFloat()*360);
                    if(!bridge)
                    {
                        Prop(assets.Props[3],new Vector3(side*3.5f,-0.08f,z+1.4f),0.45f+random.NextFloat()*0.35f,j*71);
                        Prop(assets.Props[6],new Vector3(side*3.2f,-0.07f,z+0.7f),0.38f,j*47);
                    }
                }
                // Silhouette rocks beyond the garden, never inside the playable path.
                Prop(assets.Props[5],new Vector3(side*(13+variant%3),-0.25f,length*0.55f),9+variant%4,side*48);
                Prop(assets.Props[4],new Vector3(side*7.5f,-0.1f,length*0.18f),1.4f,variant*39);
                if(bridge)
                    for(float z=2;z<length;z+=6)
                        Box("Water glint",new Vector3(side*4.7f,-0.386f,z),new Vector3(1.3f,0.01f,0.055f),ParkTheme.Hex(0xBDE2CE));
            }
            // Three paving bands imply lanes without highway markings.
            for(int lane=-1;lane<=1;lane++)
                for(float z=0;z<length;z+=4)
                {
                    float segment=Mathf.Min(4,length-z);
                    Box("Paving",new Vector3(lane*TrackMetrics.LaneWidth,-0.105f,z+segment/2),
                        new Vector3(1.58f,0.20f,segment-0.035f),
                        bridge ? ParkTheme.Hex(0xD6BE94) : (lane==0 ? ParkTheme.Paper : ParkTheme.Hex(0xE9DFCB)));
                }

            if(d.HasTag(ChunkTag.Tunnel))
                for(float z=3;z<length;z+=7)
                {
                    foreach(int side in new[]{-1,1})
                        Box("Pergola column",new Vector3(side*2.8f,1.7f,z),new Vector3(0.28f,3.4f,0.35f),ParkTheme.Ink);
                    Box("Pergola lintel",new Vector3(0,3.4f,z),new Vector3(5.9f,0.3f,0.5f),ParkTheme.Ink);
                    Prop(assets.Props[3],new Vector3(-2.3f,3.45f,z),0.9f,0);
                    Prop(assets.Props[3],new Vector3(2.3f,3.45f,z),0.9f,180);
                }

            foreach(var placement in d.Obstacles) Obstacle(placement);
            Combine(d.ChunkId);
        }

        void Obstacle(ObstaclePlacement placement)
        {
            var b=TrackGeometry.Obstacle(placement,0);
            var centre=new Vector3(b.CenterX,b.CenterY,b.CenterZ);
            bool full=placement.Kind==ObstacleKind.FullBlock;
            Box(full?"Coral planter":"Jump hurdle",centre,new Vector3(b.HalfX*2,b.HalfY*2,b.HalfZ*2),
                full?ParkTheme.Coral:ParkTheme.Gold);
            Box("Obstacle foot",new Vector3(b.CenterX,0.075f,b.CenterZ),new Vector3(b.HalfX*2,0.15f,b.HalfZ*2),ParkTheme.Ink);
            // Direction is communicated by silhouette as well as color.
            if(full)
            {
                Box("Planter rim",new Vector3(b.CenterX,b.HalfY*2-0.09f,b.CenterZ),new Vector3(b.HalfX*2,0.18f,b.HalfZ*2),ParkTheme.Paper);
                Box("Planter inset",new Vector3(b.CenterX,b.HalfY*2+0.002f,b.CenterZ),new Vector3(0.95f,0.006f,0.64f),ParkTheme.Leaf);
                for(int side=-1;side<=1;side+=2)
                {
                    for(int segment=-1;segment<=1;segment+=2)
                    {
                        var line=Box("Dodge chevron",new Vector3(b.CenterX+side*0.28f,0.96f+segment*0.09f,b.CenterZ-b.HalfZ-0.008f),new Vector3(0.26f,0.065f,0.015f),ParkTheme.Paper);
                        line.localRotation=Quaternion.Euler(0,0,-side*segment*42);
                    }
                }
            }
            else
            {
                Box("Hurdle cap",new Vector3(b.CenterX,0.445f,b.CenterZ),new Vector3(b.HalfX*2,0.11f,b.HalfZ*2),ParkTheme.Paper);
                for(int side=-1;side<=1;side+=2)
                {
                    var line=Box("Jump chevron",new Vector3(b.CenterX+side*0.105f,0.265f,b.CenterZ-b.HalfZ-0.008f),new Vector3(0.27f,0.06f,0.015f),ParkTheme.Ink);
                    line.localRotation=Quaternion.Euler(0,0,-side*38);
                }
            }
        }

        Transform Box(string name, Vector3 p, Vector3 size, Color c) => ParkTheme.Box(transform,name,p,size,c);

        void Prop(GameObject prefab, Vector3 position, float height, float angle)
        {
            var prop=Instantiate(prefab,transform,false).transform;
            prop.localPosition=position;
            prop.localScale=Vector3.one*height; // Editor-generated prop prefabs are exactly one metre tall.
            prop.localRotation=Quaternion.Euler(0,angle,0);
        }

        void Combine(string chunkId)
        {
            var temporary = new List<GameObject>();
            foreach (Transform child in transform) temporary.Add(child.gameObject);
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();
                if(renderer==null || filter.sharedMesh==null) continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<filter.sharedMesh.subMeshCount;i++)
                {
                    var material=materials[Mathf.Min(i,materials.Length-1)];
                    if(!groups.TryGetValue(material,out var list)) groups[material]=list=new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh=filter.sharedMesh,subMeshIndex=i,
                        transform=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix });
                }
                renderer.enabled=false;
            }
            var batches = new List<Batch>();
            foreach(var group in groups)
            {
                var mesh=new Mesh { name="Park chunk batch",indexFormat=IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(),true,true);
                mesh.UploadMeshData(true);
                var batch = new Batch { Mesh = mesh, Material = group.Key };
                batches.Add(batch);
                AddBatch(batch);
            }
            Cache[chunkId] = batches.ToArray();
            foreach (var go in temporary)
                if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        void AddBatch(Batch batch)
        {
            var go = new GameObject("Batch_" + batch.Material.name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = batch.Mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
