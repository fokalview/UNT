using System;
using System.IO;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
namespace Clarenza {
 public sealed class PersonalReadAloud:MonoBehaviour {
  [Serializable] class RequestData {public string text;public int rate;}
  public bool Open,Loading,Paused;
  public string Text="",Message="Paste text or load material, then choose Listen.";
  public int Rate;
  public string ExportDirectory;
  public Func<string> Passage,Notes,Answer;
  public string Endpoint="http://127.0.0.1:47831/speech";
  AudioSource source;UnityWebRequest pending;byte[] wav;int revision;Vector2 scroll;
  public bool HasAudio {get{return wav!=null&&source!=null&&source.clip!=null;}}
  public bool IsPlaying {get{return source!=null&&source.isPlaying;}}
  void Awake(){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;}
  public void Stop(){revision++;if(pending!=null)pending.Abort();pending=null;Loading=false;Paused=false;source.Stop();Message="Stopped. The lecture remains paused.";}
  void ClearAudio(){wav=null;if(source!=null&&source.clip!=null){Destroy(source.clip);source.clip=null;}}
  public void ResetPrivate(){Stop();ClearAudio();Text="";Rate=0;Open=false;ExportDirectory=null;Message="Paste text or load material, then choose Listen.";}
  public void SetText(string value){Stop();ClearAudio();Text=value??"";Message="Text loaded. Choose Listen to create audio.";}
  public void Generate(){
   Stop();ClearAudio();
   if(string.IsNullOrWhiteSpace(Text)||Text.Length>5000){Message="Enter between 1 and 5,000 characters. Paste a shorter excerpt if needed.";return;}
   Loading=true;Message="Creating audio locally… Stop cancels playback of this request.";StartCoroutine(CreateAudio(revision,Text,Rate));
  }
  IEnumerator CreateAudio(int ticket,string value,int rate){
   using(var request=new UnityWebRequest(Endpoint,"POST")){
    request.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new RequestData{text=value,rate=rate})));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");request.timeout=75;pending=request;
    yield return request.SendWebRequest();
    if(ticket!=revision)yield break;
    pending=null;Loading=false;
    if(request.isNetworkError||request.isHttpError){Message="Read-aloud unavailable. Start the Clarenza launcher and try again.";try{var error=JsonUtility.FromJson<ServiceError>(request.downloadHandler.text);if(!string.IsNullOrEmpty(error.error))Message=error.error;}catch{}yield break;}
    try{var bytes=Convert.FromBase64String(JsonUtility.FromJson<SpeechResponse>(request.downloadHandler.text).wav);var clip=LessonImport.Wav(bytes);wav=bytes;source.clip=clip;source.Play();Message="Playing personal audio. The lecture remains paused.";}
    catch(Exception){ClearAudio();Message="The service returned unusable audio. Try again.";}
   }
  }
  public void TogglePause(){if(!HasAudio)return;if(Paused){source.UnPause();Paused=false;Message="Playing personal audio.";}else if(source.isPlaying){source.Pause();Paused=true;Message="Personal audio paused.";}}
  public string Export(){
   if(!HasAudio){Message="Create audio with Listen before exporting.";return null;}
   try{if(string.IsNullOrEmpty(ExportDirectory))throw new IOException();Directory.CreateDirectory(ExportDirectory);string path=Path.Combine(ExportDirectory,"Clarenza-audio-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".wav");using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write))file.Write(wav,0,wav.Length);Message="WAV saved: "+path;return path;}
   catch(Exception){Message="Could not save the WAV. Check available disk space and folder permissions.";return null;}
  }
  bool Button(string label,GUIStyle style){return GUILayout.Button(label,style,GUILayout.MinHeight(42));}
  public void Draw(float w,float h,Texture2D panel,GUIStyle heading,GUIStyle label,GUIStyle input,GUIStyle button){
   float width=Mathf.Min(850,w-60);GUI.DrawTexture(new Rect((w-width)/2,98,width,h-180),panel);
   GUILayout.BeginArea(new Rect((w-width)/2+22,115,width-44,h-215));scroll=GUILayout.BeginScrollView(scroll);
   GUILayout.Label("Read aloud",heading);GUILayout.Label("Local Windows voice • personal audio • WAV export",label);
   GUILayout.BeginHorizontal();if(Button("Load passage",button))SetText(Passage==null?"":Passage());if(Button("Load my notes",button))SetText(Notes==null?"":Notes());if(Button("Load tutor answer",button))SetText(Answer==null?"":Answer());GUILayout.EndHorizontal();
   GUILayout.Label("Text to read (maximum 5,000 characters)",label);string edit=GUILayout.TextArea(Text,input,GUILayout.Height(210));if(edit!=Text)SetText(edit);
   GUILayout.Label(Text.Length+" / 5,000 characters",label);
   int choice=GUILayout.Toolbar(Rate<0?0:Rate>0?2:1,new[]{"Slower","Normal","Faster"},button);int newRate=(choice-1)*2;if(newRate!=Rate){Stop();ClearAudio();Rate=newRate;Message="Speed changed. Choose Listen to generate at this speed.";}
   GUILayout.BeginHorizontal();GUI.enabled=!Loading;if(Button(HasAudio?"Listen again":"Listen",button)){if(HasAudio){source.Stop();source.Play();Paused=false;Message="Playing personal audio.";}else Generate();}GUI.enabled=HasAudio&&(IsPlaying||Paused);if(Button(Paused?"Resume":"Pause",button))TogglePause();GUI.enabled=true;if(Button("Stop",button))Stop();GUI.enabled=HasAudio;if(Button("Export WAV",button))Export();GUI.enabled=true;GUILayout.EndHorizontal();
   GUILayout.Label(Message,label);GUILayout.Label("Exported files remain on this computer. Closing this panel stops personal audio; resume the lesson yourself.",label);
   if(Button("Close read-aloud",button)){Stop();Open=false;}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  void OnDestroy(){revision++;if(pending!=null)pending.Abort();ClearAudio();}
 }
}
