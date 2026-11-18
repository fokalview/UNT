using System;
using System.Collections.Generic;
using UnityEngine;
namespace Clarenza {
 [Serializable] public class TeacherRoute {public Vector3[] points;}
 public sealed class TeacherWalk:MonoBehaviour {
  public TeacherRoute[] routes;public bool lecturePlaying;public bool enabledByUser=true;public bool narrationActive;public bool focusMode;
  public bool Moving {get;private set;} public float Travelled {get;private set;}
  public bool Turning {get;private set;}
  public bool ReadyToTeach {get{return !Moving&&!Turning&&Quaternion.Angle(transform.rotation,facing)<2;}}
  public bool AtHome {get{return Vector3.Distance(transform.position,home)<.05f;}}
  public int Trips {get;private set;} public float Speed=0.6f;
  Vector3 home;Quaternion facing;TeacherPresentation presentation;List<Vector3> path=new List<Vector3>();int index;float wait=2;bool testing,returning,previousFocus;float velocity;System.Random random=new System.Random();
  public void Initialize(TeacherRoute[] data,TeacherPresentation presenter){routes=data;presentation=presenter;home=transform.position;facing=transform.rotation;}
  public void BeginTest(){testing=true;wait=0;lecturePlaying=true;Speed=1.0f;Travelled=0;Trips=0;path.Clear();index=0;returning=false;}
  public float Floor(Vector3 point){
   float best=float.NegativeInfinity;
   foreach(var h in Physics.RaycastAll(point+Vector3.up*2,Vector3.down,5))if(h.collider.gameObject.name.StartsWith("Teacher walk surface")&&h.normal.y>.7f)best=Mathf.Max(best,h.point.y);
   return float.IsNegativeInfinity(best)?home.y:best;
  }
  void Plan(){
   if(routes==null||routes.Length==0)return;
   var pts=routes[testing?0:random.Next(routes.Length)].points;
   if(pts==null||pts.Length<2)return;
   int top=testing?pts.Length-1:random.Next(Math.Max(1,pts.Length/2),pts.Length);
   path.Clear();path.Add(home+Vector3.forward*.75f);path.Add(new Vector3(pts[0].x,home.y,home.z+.75f));
   for(int i=0;i<=top;i++)path.Add(pts[i]);
   for(int i=top-1;i>=0;i--)path.Add(pts[i]);
   path.Add(new Vector3(pts[0].x,home.y,home.z+.75f));path.Add(home+Vector3.forward*.75f);path.Add(home);index=0;
  }
  void ReturnHome(){
   if(AtHome){path.Clear();index=0;return;}
   // Retrace visited waypoints, never cut diagonally through desks or seats.
   var back=new List<Vector3>();
   for(int i=Math.Min(index-1,path.Count-1);i>=0;i--)back.Add(path[i]);
   back.Add(home);path=back;index=0;returning=true;wait=0;
  }
  void Face(Quaternion target){
   float before=Quaternion.Angle(transform.rotation,target);
   transform.rotation=Quaternion.RotateTowards(transform.rotation,target,85*Time.deltaTime);
   Turning=before>1;
  }
  void Update(){
   Moving=false;Turning=false;
   if(focusMode&&!previousFocus)ReturnHome();previousFocus=focusMode;
   if(!lecturePlaying){velocity=0;Face(facing);return;}
   if(!enabledByUser||presentation.IsPresenting||narrationActive){velocity=0;Face(facing);return;}
   if(focusMode&&AtHome){velocity=0;Face(facing);return;}
   if(wait>0){velocity=0;wait-=Time.deltaTime;Face(facing);return;}
   if(index>=path.Count){Plan();if(path.Count==0)return;}
   Vector3 delta=path[index]-transform.position;
   if(delta.magnitude<.025f){
    index++;
    if(index>=path.Count){Trips++;wait=testing?999:random.Next(12,21);returning=false;}
    else if(!returning&&index==path.Count/2)wait=testing?0:random.Next(4,8);
    return;
   }
   Vector3 flat=new Vector3(delta.x,0,delta.z);
   if(flat.sqrMagnitude>.0001f){
    var desired=Quaternion.LookRotation(-flat.normalized);Face(desired);
    if(Quaternion.Angle(transform.rotation,desired)>12){velocity=0;return;}
   }
   velocity=Mathf.MoveTowards(velocity,Speed,Time.deltaTime*.9f);
   var previous=transform.position;transform.position=Vector3.MoveTowards(previous,path[index],velocity*Time.deltaTime);
   Travelled+=Vector3.Distance(previous,transform.position);Moving=true;
  }
 }
}
