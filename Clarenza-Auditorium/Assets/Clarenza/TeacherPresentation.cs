using UnityEngine;
namespace Clarenza {
 // Procedural presentation and grounded stepping on the preserved humanoid rig.
 public sealed class TeacherPresentation:MonoBehaviour {
  TeacherWalk walk;
  Transform lt,lk,lf,rt,rk,rf;Quaternion lt0,lk0,lf0,rt0,rk0,rf0,leftFootFacing,rightFootFacing;
  Vector3 leftPlant,rightPlant,swingStart,swingEnd;float stepTime,ankleHeight;bool leftSwing=true,wasWalking;
  public void SetWalk(TeacherWalk value){walk=value;}
  Animator rig;Transform root,visual,lu,ll,lh,ru,rl,rh,head;Quaternion l0,l1,l2,r0,r1,r2,h0,body0;
  GameObject tablet;Renderer display;float clock=99;Vector3 board;bool presenting;
  public float GestureTime {get{return clock;}}
  public bool IsPresenting {get{return presenting;}}
  public void Initialize(Transform model,Vector3 target){
   root=transform;visual=model;board=target;rig=model.GetComponentInChildren<Animator>();rig.enabled=false;
   lu=rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);ll=rig.GetBoneTransform(HumanBodyBones.LeftLowerArm);lh=rig.GetBoneTransform(HumanBodyBones.LeftHand);
   ru=rig.GetBoneTransform(HumanBodyBones.RightUpperArm);rl=rig.GetBoneTransform(HumanBodyBones.RightLowerArm);rh=rig.GetBoneTransform(HumanBodyBones.RightHand);head=rig.GetBoneTransform(HumanBodyBones.Head);
   if(lu==null||ll==null||lh==null||ru==null||rl==null||rh==null)throw new System.Exception("Teacher arm rig incomplete");
   lt=rig.GetBoneTransform(HumanBodyBones.LeftUpperLeg);lk=rig.GetBoneTransform(HumanBodyBones.LeftLowerLeg);lf=rig.GetBoneTransform(HumanBodyBones.LeftFoot);
   rt=rig.GetBoneTransform(HumanBodyBones.RightUpperLeg);rk=rig.GetBoneTransform(HumanBodyBones.RightLowerLeg);rf=rig.GetBoneTransform(HumanBodyBones.RightFoot);
   lt0=lt.localRotation;lk0=lk.localRotation;lf0=lf.localRotation;rt0=rt.localRotation;rk0=rk.localRotation;rf0=rf.localRotation;
   ankleHeight=Mathf.Max(.06f,(lf.position.y+rf.position.y)*.5f-root.position.y);leftFootFacing=Quaternion.Inverse(root.rotation)*lf.rotation;rightFootFacing=Quaternion.Inverse(root.rotation)*rf.rotation;
   l0=lu.localRotation;l1=ll.localRotation;l2=lh.localRotation;r0=ru.localRotation;r1=rl.localRotation;r2=rh.localRotation;h0=head.localRotation;body0=visual.localRotation;
   tablet=GameObject.CreatePrimitive(PrimitiveType.Cube);tablet.name="Professor tablet";Destroy(tablet.GetComponent<Collider>());tablet.transform.SetParent(root,false);tablet.transform.localScale=new Vector3(.30f,.018f,.22f);
   var mat=new Material(Shader.Find("Standard"));mat.color=new Color(.06f,.08f,.1f);mat.SetFloat("_Glossiness",.65f);tablet.GetComponent<Renderer>().material=mat;
   var screen=GameObject.CreatePrimitive(PrimitiveType.Cube);screen.name="Tablet screen";Destroy(screen.GetComponent<Collider>());screen.transform.SetParent(tablet.transform,false);screen.transform.localPosition=new Vector3(0,.6f,0);screen.transform.localScale=new Vector3(.88f,.08f,.86f);display=screen.GetComponent<Renderer>();var light=new Material(Shader.Find("Standard"));light.color=new Color(.12f,.35f,.43f);light.EnableKeyword("_EMISSION");light.SetColor("_EmissionColor",new Color(.04f,.15f,.2f));display.material=light;
  }
  public void Present(){clock=0;presenting=true;}
  float Ease(float a,float b,float t){return Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));}
  void LateUpdate(){if(rig==null)return;if(!presenting||walk==null||walk.ReadyToTeach)clock+=Time.deltaTime;if(clock>2.8f)presenting=false;
   lu.localRotation=l0;ll.localRotation=l1;lh.localRotation=l2;ru.localRotation=r0;rl.localRotation=r1;rh.localRotation=r2;head.localRotation=h0;
   float turn=presenting?Ease(.75f,1.5f,clock)*(1-Ease(1.9f,2.8f,clock)):0;
   visual.localRotation=body0; // Keep hips and planted feet stable during the arm gesture.
   Vector3 forward=visual.forward,right=visual.right,up=Vector3.up,origin=root.position;
   float breath=Mathf.Sin(Time.time*.9f)*.004f;
   Vector3 tabletCenter=origin-right*.20f+forward*.28f+up*(1.12f+breath);
   tablet.transform.position=tabletCenter;tablet.transform.rotation=Quaternion.LookRotation(forward,up)*Quaternion.Euler(12,0,0);
   Solve(lu,ll,lh,tabletCenter-up*.025f-forward*.06f,origin-right*.5f+forward*.05f+up*.88f);
   var finger=rig.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);if(finger!=null)lh.rotation=Quaternion.FromToRotation(finger.position-lh.position,forward)*lh.rotation;
   Vector3 resting=origin+right*.29f+forward*.13f+up*(.97f+breath);
   Vector3 tapping=tabletCenter+up*.10f+right*.03f;
   float reach=presenting?Ease(0,.45f,clock)*(1-Ease(.65f,1.2f,clock)):0;
   Vector3 target=Vector3.Lerp(resting,tapping,reach);
   float send=presenting?Ease(.85f,1.45f,clock)*(1-Ease(1.8f,2.8f,clock)):0;
   Vector3 toward=(board-Vector3.up*.65f-ru.position).normalized;
   Vector3 release=ru.position+toward*.40f;
   target=Vector3.Lerp(target,release,send);
   Solve(ru,rl,rh,target,origin+right*.56f+forward*.1f+up*1.03f);
   head.localRotation=h0*Quaternion.Euler(0,-12*turn,0);
   StepLegs(forward,right);
   display.material.SetColor("_EmissionColor",new Color(.04f,.15f,.2f)*(1+2*reach));
  }
  Vector3 Ground(Vector3 p){p.y=walk.Floor(p)+ankleHeight;return p;}
  void StepLegs(Vector3 forward,Vector3 right){
   if(walk==null)return;
   lt.localRotation=lt0;lk.localRotation=lk0;lf.localRotation=lf0;rt.localRotation=rt0;rk.localRotation=rk0;rf.localRotation=rf0;
   if(!walk.Moving&&!walk.Turning){
    var leftRest=Ground(root.position-right*.12f);var rightRest=Ground(root.position+right*.12f);
    if(leftPlant==Vector3.zero){leftPlant=leftRest;rightPlant=rightRest;}
    leftPlant=Vector3.MoveTowards(leftPlant,leftRest,Time.deltaTime*.65f);
    rightPlant=Vector3.MoveTowards(rightPlant,rightRest,Time.deltaTime*.65f);wasWalking=false;
   }
   else{
    if(!wasWalking){leftPlant=Ground(lf.position);rightPlant=Ground(rf.position);stepTime=0;leftSwing=true;swingStart=leftPlant;swingEnd=Ground(root.position-right*.12f+forward*(walk.Moving?.28f:0));wasWalking=true;}
    stepTime+=Time.deltaTime;float u=Mathf.Clamp01(stepTime/.48f);Vector3 foot=Vector3.Lerp(swingStart,swingEnd,Mathf.SmoothStep(0,1,u));foot.y+=Mathf.Sin(u*Mathf.PI)*(walk.Moving?Mathf.Clamp(.08f+Mathf.Abs(swingEnd.y-swingStart.y),.08f,.24f):.06f);
    if(leftSwing)leftPlant=foot;else rightPlant=foot;
    if(u>=1){leftSwing=!leftSwing;stepTime=0;swingStart=leftSwing?leftPlant:rightPlant;swingEnd=Ground(root.position+right*(leftSwing?-.12f:.12f)+forward*(walk.Moving?.28f:0));}
   }
   Solve(lt,lk,lf,leftPlant,root.position+forward*.6f-right*.12f+Vector3.up*.5f);Solve(rt,rk,rf,rightPlant,root.position+forward*.6f+right*.12f+Vector3.up*.5f);
   lf.rotation=root.rotation*leftFootFacing;rf.rotation=root.rotation*rightFootFacing;
  }
  static void Solve(Transform upper,Transform lower,Transform hand,Vector3 target,Vector3 pole){
   Vector3 start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hand.position);
   Vector3 delta=target-start;float d=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.005f,a+b-.008f);Vector3 dir=delta.normalized;
   Vector3 bend=Vector3.ProjectOnPlane(pole-start,dir).normalized;if(bend.sqrMagnitude<.01f)bend=Vector3.Cross(dir,Vector3.up).normalized;
   float x=(a*a-b*b+d*d)/(2*d);float y=Mathf.Sqrt(Mathf.Max(0,a*a-x*x));Vector3 elbow=start+dir*x+bend*y;
   upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
   lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,start+dir*d-lower.position)*lower.rotation;
  }
  void OnDestroy(){if(tablet!=null){Destroy(tablet.GetComponent<Renderer>().material);if(display!=null)Destroy(display.material);Destroy(tablet);}}
 }
}

