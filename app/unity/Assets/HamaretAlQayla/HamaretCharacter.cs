using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Locally authored surfaces for Hamaret's articulated costume and character.</summary>
internal sealed class HamaretCharacter
{
    private readonly Func<Color, Material> material;
    private readonly Transform body, head, leftSleeve, rightSleeve, leftHock, rightHock;
    private readonly Transform leftFoot, rightFoot;
    private readonly Renderer cloth, mantle;
    private readonly Renderer[] sleeves = new Renderer[2];
    private readonly GameObject hood, hair, shoes, hooves;
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private readonly List<Mesh> meshes;

    internal HamaretCharacter(Transform root, Func<Color, Material> makeMaterial, List<Mesh> ownedMeshes)
    {
        material = makeMaterial;
        meshes = ownedMeshes;
        Material fabric = material(new Color(.044f, .042f, .040f));
        fabric.SetFloat("_Weave",1f);
        Material trimmedFabric = material(new Color(.044f, .042f, .040f));
        trimmedFabric.SetFloat("_Weave",1f);
        trimmedFabric.SetFloat("_Trim",1f);
        Material hide = material(new Color(.32f, .265f, .20f));
        hide.SetFloat("_Detail", 3);
        Material horn = material(new Color(.10f, .088f, .078f));
        horn.SetFloat("_Detail", 4);
        Material skin = material(new Color(.63f, .405f, .287f));
        Material complexion = material(new Color(.63f, .405f, .287f));
        complexion.SetFloat("_Detail", 1);
        Material eye = material(Color.white);
        eye.SetFloat("_Detail", 2);
        Material ink = material(new Color(.081f, .060f, .044f));
        Material gold = material(new Color(.90f, .65f, .28f));
        body = Joint(root, "Torso and abaya", Vector3.zero);
        cloth = Loft(body, "Abaya", trimmedFabric, new[] {
            P(.18f,.365f,.233f,-.047f), P(.21f,.36f,.226f,-.043f), P(.36f,.325f,.208f,-.033f), P(.62f,.28f,.176f,-.018f),
            P(.86f,.221f,.145f), P(1.02f,.166f,.120f), P(1.16f,.176f,.141f,.013f),
            P(1.28f,.204f,.134f), P(1.36f,.181f,.099f), P(1.41f,.074f,.061f)
        }, 64, 6, .19f, hemShape: true);
        mantle = Loft(body, "Draped shoulder shawl", fabric, new[] {
            P(.89f,.25f,.156f,-.058f), P(1.05f,.26f,.175f,-.038f), P(1.19f,.258f,.170f,-.020f),
            P(1.27f,.246f,.149f,-.011f), P(1.33f,.216f,.131f), P(1.39f,.163f,.110f),
            P(1.47f,.082f,.071f)
        }, 58, 6, .17f, opening: 1.20f, hemShape: true);
        Bead(body, "Abaya clasp", gold, new Vector3(.057f,1.292f,.151f), new Vector3(.025f,.028f,.013f));
        for (int side = -1; side <= 1; side += 2)
        {
            Transform arm = Joint(body, side < 0 ? "Left arm" : "Right arm", new Vector3(side*.212f,1.33f,0));
            sleeves[side < 0 ? 0 : 1] = Loft(arm,"Tapered sleeve", trimmedFabric, new[]{ P(-.51f,.086f,.076f), P(-.40f,.087f,.075f),
                P(-.23f,.071f,.067f), P(-.10f,.077f,.068f), P(-.009f,.078f,.062f), P(.050f,.035f,.040f) }, 32, 5,.15f);
            Bead(arm,"Hand",skin,new Vector3(0,-.555f,.016f),new Vector3(.080f,.129f,.056f));
            Bead(arm,"Thumb",skin,new Vector3(-side*.034f,-.551f,.039f),new Vector3(.031f,.073f,.031f));
            if (side < 0) leftSleeve = arm; else rightSleeve = arm;
        }

        head = Joint(body,"Neck and gaze",new Vector3(0,1.385f,0));
        Bead(head,"Neck",skin,new Vector3(0,.045f,.01f),new Vector3(.104f,.16f,.102f));
        Loft(head,"Sculpted face",complexion,new[]{
            P(.065f,.023f,.025f,.036f), P(.079f,.044f,.047f,.033f), P(.102f,.058f,.057f,.027f),
            P(.137f,.071f,.065f,.023f), P(.165f,.080f,.071f,.013f), P(.189f,.079f,.072f,.009f),
            P(.211f,.076f,.076f,.008f),P(.244f,.075f,.072f,.008f),
            P(.276f,.064f,.058f,.002f), P(.302f,.036f,.038f,-.003f),P(.313f,.002f,.002f,-.004f)
        }, 72, 6,0, faceShape:true);
        for (int side = -1; side <= 1; side += 2)
        {
            MakeEye(head, eye, side);
        }

        hood = Joint(head,"Folded head covering",Vector3.zero).gameObject;
        Loft(hood.transform,"Hood drape",fabric,new[]{P(-.23f,.210f,.178f,-.039f), P(-.15f,.208f,.172f,-.028f),
            P(-.052f,.157f,.149f,-.021f),P(.030f,.113f,.127f,-.011f),P(.135f,.093f,.105f,-.005f),
            P(.224f,.096f,.106f,-.010f),P(.294f,.078f,.093f,-.012f),P(.327f,.050f,.066f,-.013f),
            P(.345f,.002f,.004f,-.008f)}, 64,6,.10f, 2.29f);
        hair = Joint(head,"Revealed hair",Vector3.zero).gameObject;
        Loft(hair.transform,"Swept dark hair",ink,new[]{P(-.04f,.100f,.071f,-.055f),
            P(.105f,.108f,.096f,-.050f),P(.237f,.104f,.093f,-.032f),P(.292f,.084f,.081f,-.005f),
            P(.345f,.014f,.024f)},36,4,.085f,2.65f);
        for(int side=-1; side<=1; side+=2)
        {
            Bead(hair.transform,"Gold earring",gold,new Vector3(side*.091f,.121f,.021f),new Vector3(.023f,.043f,.020f));
        }
        hair.SetActive(false);

        hooves = Joint(root,"Donkey legs",Vector3.zero).gameObject;
        shoes = Joint(root,"Human shoes",Vector3.zero).gameObject;
        for (int side=-1; side<=1; side+=2)
        {
            Transform hock=Joint(hooves.transform,side<0?"Left hock":"Right hock",new Vector3(side*.121f,.39f,0));
            Loft(hock,"Shaped fetlock",hide,new[]{P(-.31f,.043f,.046f,.038f),P(-.245f,.044f,.041f,.021f),
                P(-.164f,.039f,.047f,-.037f),P(-.083f,.056f,.058f,-.079f),
                P(.04f,.064f,.067f,-.031f)},26,4,.052f);
            Loft(hock,"Tapered fetlock coat",hide,new[]{P(-.346f,.040f,.051f,.043f),
                P(-.310f,.049f,.051f,.037f),P(-.265f,.050f,.050f,.025f),
                P(-.215f,.049f,.048f,-.002f),P(-.166f,.043f,.052f,-.034f)}, 50, 5, .24f);
            Transform foot=Joint(hock,side<0?"Left hoof":"Donkey hoof",new Vector3(0,-.344f,.053f));
            Loft(foot,"Solid equine hoof wall",horn,new[]{P(-.044f,.061f,.090f,.018f),
                P(-.040f,.064f,.092f,.018f),P(-.024f,.064f,.092f,.015f),P(-.002f,.058f,.085f,.007f),
                P(.022f,.051f,.068f,-.002f),P(.052f,.039f,.044f,-.008f)},48,4,.026f);
            Loft(foot,"Hoof coronary band",hide,new[]{P(.039f,.043f,.052f),P(.058f,.038f,.044f)},24,3,.033f);
            Bead(shoes.transform,"Embroidered slipper",gold,new Vector3(side*.112f,.039f,.086f),
                new Vector3(.129f,.078f,.226f));
            if(side<0){leftHock=hock;leftFoot=foot;}else{rightHock=hock;rightFoot=foot;}
        }
        shoes.SetActive(false);
    }

    internal void Pose(float gait, float stride, float stomp, float release, float bow)
    {
        float time = Time.time;
        float breath = Mathf.Sin(time * 2.1f);
        float wave = Mathf.Sin(gait);
        float sway = breath*.8f + wave*stride*5f;
        body.localRotation = Quaternion.Euler(5f*stride - 7f*stomp + 17f*bow, 0, sway);
        body.localPosition = Vector3.up * (.004f*breath - .027f*stomp);
        head.localRotation = Quaternion.Euler(-3f + bow*9f, breath*3f, -sway*.6f);
        leftSleeve.localRotation = Quaternion.Euler(wave*stride*26f, 0, -11f - stomp*10f);
        rightSleeve.localRotation = Quaternion.Euler(-wave*stride*26f - bow*58f, -bow*19f, 11f + stomp*15f);
        leftHock.localRotation = Quaternion.Euler(wave*stride*36f,0,0);
        rightHock.localRotation = Quaternion.Euler(-wave*stride*36f - 44f*stomp,0,0);
        leftFoot.localPosition = new Vector3(0,-.344f + Mathf.Max(0,-wave)*stride*.12f,.053f);
        rightFoot.localPosition = new Vector3(0,-.344f + Mathf.Max(0,wave)*stride*.12f + stomp*.045f,.053f);
        rightFoot.localRotation = Quaternion.Euler(stomp*18f,0,0);
        float fabricMotion = .15f + stride*.79f + stomp*.49f + Mathf.Sin(Mathf.PI*release)*.65f;
        Color fabric = Color.Lerp(new Color(.044f,.042f,.040f),new Color(.23f,.33f,.275f),release);
        Style(cloth,fabric,fabricMotion);
        Style(mantle,fabric,fabricMotion*.55f);
        foreach (Renderer sleeve in sleeves)
            Style(sleeve,fabric,0);
        float retreat = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.67f,release));
        hood.transform.localRotation = Quaternion.Euler(-18f * retreat, 0, 0);
        hood.transform.localPosition = Vector3.back * (.04f * retreat);
        hood.SetActive(release < .67f);
        hair.SetActive(release >= .54f);
        hooves.transform.localScale = Vector3.Lerp(Vector3.one,new Vector3(.88f,.78f,.88f),release);
        hooves.SetActive(release < .77f);
        shoes.SetActive(release >= .67f);
    }

    private void Style(Renderer renderer, Color color, float motion)
    {
        properties.SetColor("_Color",color);
        properties.SetFloat("_ClothMotion",motion);
        renderer.SetPropertyBlock(properties);
    }

    private static Vector4 P(float y,float rx,float rz,float z=0) => new Vector4(y,rx,rz,z);

    private Renderer Bead(Transform parent,string name,Material surface,Vector3 center,Vector3 size)
    {
        var rings = new Vector4[15];
        for(int i=0;i<rings.Length;i++)
        {
            float pitch=Mathf.PI*(i/(float)(rings.Length-1)-.5f);
            float radius=Mathf.Max(.002f,Mathf.Cos(pitch))*.5f;
            rings[i]=P(Mathf.Sin(pitch)*.5f,radius,radius);
        }
        Renderer result=Loft(parent,name,surface,rings,20,1,0);
        result.transform.localPosition=center;
        result.transform.localScale=size;
        return result;
    }

    private Renderer Loft(Transform parent,string name,Material surface,Vector4[] profile,int segments,
        int subdivisions,float pleats,float opening=0,bool faceShape=false,bool hemShape=false)
    {
        var mesh = new Mesh { name = name };
        meshes.Add(mesh);
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        int rings=(profile.Length-1)*subdivisions+1;
        for(int row=0;row<rings;row++)
        {
            float t=row/(float)(rings-1);
            float step=row/(float)subdivisions;
            int section=Mathf.Min((int)step,profile.Length-2);
            var p=Vector4.Lerp(profile[section],profile[section+1],step-section);
            for(int i=0;i<=segments;i++)
            {
                float u=i/(float)segments;
                float slit=opening*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1,t)));
                float angle=slit*.5f + (Mathf.PI*2-slit)*u;
                float fold=1+pleats*(Mathf.Sin(angle*9+t*2.5f)*.66f+
                    Mathf.Sin(angle*17-t*4)*.22f)*(1-t*.67f);
                float x=Mathf.Sin(angle)*p.y*fold;
                float z=Mathf.Cos(angle)*p.z*fold+p.w;
                if(faceShape && Mathf.Cos(angle)>.45f)
                {
                    float bridge=(p.x-.173f)/.045f;
                    float tip=(p.x-.153f)/.019f;
                    float socket=(Mathf.Abs(x)-.035f)/.021f;
                    float socketY=(p.x-.189f)/.022f;
                    float cheek=(Mathf.Abs(x)-.050f)/.025f;
                    float cheekY=(p.x-.149f)/.028f;
                    z+=Mathf.Exp(-9000*x*x-bridge*bridge)*.014f+
                       Mathf.Exp(-4900*x*x-tip*tip)*.021f+
                       Mathf.Exp(-cheek*cheek-cheekY*cheekY)*.008f-
                       Mathf.Exp(-socket*socket-socketY*socketY)*.010f;
                }
                float y=p.x;
                if(hemShape)
                {
                    float drape=Mathf.Clamp01((.53f-y)/.34f);
                    y+=drape*(Mathf.Cos(angle)*.076f+Mathf.Sin(angle*3+.7f)*.027f);
                }
                vertices.Add(new Vector3(x,y,z));
                uv.Add(new Vector2(u,t));
                if(row==0 || i==0) continue;
                int v=vertices.Count-1;
                int w=v-segments-1;
                triangles.Add(w-1);triangles.Add(w);triangles.Add(v-1);
                triangles.Add(w);triangles.Add(v);triangles.Add(v-1);
            }
        }
        mesh.SetVertices(vertices);
        mesh.SetUVs(0,uv);
        mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();
        if(opening==0)
        {
            Vector3[] normals=mesh.normals;
            for(int row=0;row<rings;row++)
            {
                int first=row*(segments+1);
                Vector3 normal=(normals[first]+normals[first+segments]).normalized;
                normals[first]=normal; normals[first+segments]=normal;
            }
            mesh.normals=normals;
        }
        var go=Joint(parent,name,Vector3.zero).gameObject;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=surface;
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    private void MakeEye(Transform parent, Material surface, int side)
    {
        const int steps = 24;
        var vertices = new Vector3[(steps + 1) * 3];
        var uv = new Vector2[vertices.Length];
        var triangles = new List<int>();
        for (int i = 0; i <= steps; i++)
        {
            float u = i / (float)steps;
            float almond = Mathf.Sin(u * Mathf.PI);
            float x = side * .035f + (u - .5f) * .033f;
            float y = .188f + side * (u - .5f) * .004f;
            float z = .0715f + .008f * almond - side * (u - .5f) * .013f;
            for (int row = 0; row < 3; row++)
            {
                int index = i * 3 + row;
                vertices[index] = new Vector3(x, y + (row - 1) * .0058f * almond, z + (row == 1 ? .0018f : 0));
                uv[index] = new Vector2(u, row * .5f);
                if (i == 0 || row == 0) continue;
                triangles.Add(index-4); triangles.Add(index-1); triangles.Add(index-3);
                triangles.Add(index-3); triangles.Add(index-1); triangles.Add(index);
            }
        }
        var mesh = new Mesh { name = "Inset almond eye", vertices = vertices, uv = uv, triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        meshes.Add(mesh);
        var go = Joint(parent,"Inset eye",Vector3.zero).gameObject;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = surface;
    }

    private static Transform Joint(Transform parent,string name,Vector3 position)
    {
        var joint=new GameObject(name).transform;
        joint.SetParent(parent,false);
        joint.localPosition=position;
        return joint;
    }
}
