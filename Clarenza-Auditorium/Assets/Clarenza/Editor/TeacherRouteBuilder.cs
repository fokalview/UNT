using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace Clarenza {
 public static class TeacherRouteBuilder {
  public static TeacherRoute[] Build(){
   var result=new List<TeacherRoute>();
   foreach(var filter in UnityEngine.Object.FindObjectsOfType<MeshFilter>().Where(x=>x.name.StartsWith("Stair "))){
    var mesh=filter.sharedMesh;var v=mesh.vertices;var tri=mesh.triangles;
    var groups=new Dictionary<int,List<Vector3>>();
    for(int i=0;i<tri.Length;i+=3){var a=filter.transform.TransformPoint(v[tri[i]]);var b=filter.transform.TransformPoint(v[tri[i+1]]);var c=filter.transform.TransformPoint(v[tri[i+2]]);var cross=Vector3.Cross(b-a,c-a);if(cross.normalized.y<.95f||cross.magnitude<.01f)continue;int key=Mathf.RoundToInt((a.y+b.y+c.y)/3*100);if(!groups.ContainsKey(key))groups[key]=new List<Vector3>();groups[key].Add(a);groups[key].Add(b);groups[key].Add(c);}
    var points=groups.OrderBy(x=>x.Key).Select(x=>{var a=x.Value;return new Vector3((a.Min(p=>p.x)+a.Max(p=>p.x))*.5f,x.Key/100f,(a.Min(p=>p.z)+a.Max(p=>p.z))*.5f);}).ToArray();
    if(points.Length<3)throw new Exception("No tread route on "+filter.name);
    for(int i=1;i<points.Length;i++)if(Vector3.Distance(points[i],points[i-1])>1.5f)throw new Exception("Discontinuous stairs: "+filter.name);
    var surface=new GameObject("Teacher walk surface "+filter.name);surface.transform.SetPositionAndRotation(filter.transform.position,filter.transform.rotation);surface.transform.localScale=filter.transform.lossyScale;surface.AddComponent<MeshCollider>().sharedMesh=mesh;
    result.Add(new TeacherRoute{points=points});Debug.Log("TEACHER_ROUTE "+filter.name+" treads="+points.Length+" bottom="+points[0]+" top="+points[points.Length-1]);
   }
   if(result.Count==0)throw new Exception("No stairs found");return result.ToArray();
  }
 }
}
