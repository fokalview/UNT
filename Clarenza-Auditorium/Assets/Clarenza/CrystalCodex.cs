using UnityEngine;
namespace Clarenza {
 public sealed class CrystalCodex:MonoBehaviour {
  Camera bookCamera;Transform left,right;float openness;bool opened;
  public bool Ready {get{return openness>.985f;}}
  Material glass,metal,page,gem;
  Material Mat(Color c,bool glow=false){var m=new Material(Shader.Find("Standard"));m.color=c;m.SetFloat("_Glossiness",.65f);if(glow){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*.35f);}return m;}
  void Part(string name,Transform parent,Vector3 pos,Vector3 size,Material mat){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.layer=30;Destroy(g.GetComponent<Collider>());g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;}
  void Jewel(Transform parent,Vector3 pos,float size){var g=new GameObject("Faceted crystal");g.layer=30;g.transform.SetParent(parent,false);g.transform.localPosition=pos;var mesh=new Mesh();mesh.vertices=new[]{new Vector3(0,size,0),new Vector3(size*.6f,0,0),new Vector3(0,-size,0),new Vector3(-size*.6f,0,0),new Vector3(0,0,-size*.5f),new Vector3(0,0,size*.3f)};mesh.triangles=new[]{0,4,1,1,4,2,2,4,3,3,4,0,0,1,5,1,2,5,2,3,5,3,0,5};mesh.RecalculateNormals();g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=gem;}
  public void Initialize(Camera main){
   main.cullingMask&=~(1<<30);transform.position=new Vector3(0,-200,0);
   glass=Mat(new Color(.045f,.18f,.28f),true);metal=Mat(new Color(.5f,.68f,.75f));page=Mat(new Color(.055f,.11f,.16f),true);gem=Mat(new Color(.12f,.8f,1),true);
   var go=new GameObject("Codex presentation camera");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,0,-12);bookCamera=go.AddComponent<Camera>();bookCamera.orthographic=true;bookCamera.orthographicSize=3.35f;bookCamera.clearFlags=CameraClearFlags.Depth;bookCamera.cullingMask=1<<30;bookCamera.depth=10;bookCamera.nearClipPlane=.1f;bookCamera.farClipPlane=30;
   var lightObj=new GameObject("Codex key light");lightObj.transform.SetParent(transform,false);lightObj.transform.localPosition=new Vector3(0,2,-5);var light=lightObj.AddComponent<Light>();light.type=LightType.Point;light.range=15;light.intensity=1.4f;light.cullingMask=1<<30;
   left=new GameObject("Left hinged cover").transform;left.SetParent(transform,false);right=new GameObject("Right hinged cover").transform;right.SetParent(transform,false);
   foreach(var side in new[]{-1,1}){var parent=side<0?left:right;float center=side*2.36f;
    Part("Crystal cover",parent,new Vector3(center,0,.08f),new Vector3(4.65f,5.85f,.13f),glass);
    Part("Reading surface",parent,new Vector3(center,0,-.01f),new Vector3(4.38f,5.5f,.06f),page);
    foreach(float y in new[]{-2.87f,2.87f})Part("Silver rim",parent,new Vector3(center,y,-.02f),new Vector3(4.63f,.035f,.08f),metal);
    Part("Outer illuminated edge",parent,new Vector3(side*4.65f,0,-.03f),new Vector3(.035f,5.72f,.09f),gem);
    foreach(float y in new[]{-2.72f,2.72f})Jewel(parent,new Vector3(side*4.47f,y,-.1f),.15f);
   }
   Part("Metal spine",transform,new Vector3(0,0,0),new Vector3(.12f,5.85f,.2f),metal);Jewel(transform,new Vector3(0,2.92f,-.1f),.3f);Jewel(transform,new Vector3(0,-2.92f,-.1f),.22f);
  }
  public void SetOpen(bool value){opened=value;}
  void LateUpdate(){if(bookCamera==null)return;openness=Mathf.MoveTowards(openness,opened?1:0,Time.unscaledDeltaTime*3);left.localRotation=Quaternion.Euler(0,Mathf.Lerp(-82,0,openness),0);right.localRotation=Quaternion.Euler(0,Mathf.Lerp(82,0,openness),0);float angle=35*(1-openness);bookCamera.transform.localPosition=new Vector3(-12*Mathf.Sin(angle*Mathf.Deg2Rad),0,-12*Mathf.Cos(angle*Mathf.Deg2Rad));bookCamera.transform.localRotation=Quaternion.Euler(0,angle,0);bookCamera.rect=opened||openness>.01f?new Rect(.12f,.22f,.76f,.64f):new Rect(.84f,.23f,.14f,.19f);}
  public Rect PageRect(bool leftPage,float scale){float x=leftPage?-4.3f:.35f;var a=bookCamera.WorldToScreenPoint(transform.position+new Vector3(x,2.48f,-.12f));var b=bookCamera.WorldToScreenPoint(transform.position+new Vector3(x+3.95f,-2.48f,-.12f));return new Rect(a.x/scale,(Screen.height-a.y)/scale,(b.x-a.x)/scale,(a.y-b.y)/scale);}
  void OnDestroy(){if(glass!=null)Destroy(glass);if(metal!=null)Destroy(metal);if(page!=null)Destroy(page);if(gem!=null)Destroy(gem);}
 }
}
