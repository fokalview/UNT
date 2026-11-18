using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace Clarenza {
 public sealed class AuditoriumApp:MonoBehaviour {
  public Material presenterMaterial;
  public GameObject teacherPrefab;
  public TeacherRoute[] walkRoutes;
  TeacherWalk teacherWalk;
  bool walkingTest;
  public Vector3 boardCenter;
  public Vector3[] seatPositions;
  Camera eye;
  CrystalCodex codex;
  TeacherPresentation teacherMotion;
  bool boardPending;
  Transform professor,arm;
  AudioSource speech;
  PersonalReadAloud readAloud;
  TextMesh board;
  GameObject crowd;
  LessonDocument lesson;
  string screen="lobby",status="",filePath="",draft="",notes="",question="",answer="",style="Step by step";
  string subject="", search="";
  string lobbyPage="home";int libraryTab;
  Texture2D homeBackground,homeAccent;
  [Serializable] class Account {public string token,id,username,role;}
  [Serializable] class Credentials {public string username,password,role;}
  [Serializable] class CatalogItem {public string id,title,scope,ownerId,approval,classType;public int version;}
  [Serializable] class CatalogResult {public CatalogItem[] items;}
  [Serializable] class IdRequest {public string id;}
  [Serializable] class SaveRequest {public string scope,parentId;public bool reviewed;public LessonDocument document;}
  [Serializable] class SaveResult {public LessonDocument document;}
  [Serializable] class ClassRequest {public string topic,level,language;public int minutes;public string[] sources;}
  [Serializable] class LessonProgress {public int section,quizIndex,selected,score;public bool submitted;public float clock;}
  float progressSavedAt;
  string requestedLanguage="English";int requestedLevel,requestedMinutes=15;
  Account account;
  CatalogItem[] sharedCatalog=new CatalogItem[0];
  string loginName="",loginPassword="",newName="",newPassword="";
  int newRole=2;
  bool studyMode,readOnlyLesson;
  bool IsSmoke {get{return Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-test")>=0;}}
  bool CanTeach {get{return account!=null&&(account.role=="admin"||account.role=="teacher");}}

  int section,quizIndex,selected=-1,score,mode,bookTab,lookback=10,speechVersion;
  float passageQuietTime;
  bool busy,book,playing,submitted,marking,boardFocus,speechLoading;
  float sectionTime,clock,markStart,yaw,pitch;
  Vector3 seat;
  ServiceHealth health=new ServiceHealth();
  Vector2 scroll,transcriptScroll;
  List<string> transcript=new List<string>();
  List<float> transcriptTimes=new List<float>();
  List<string> catalog=new List<string>();
  GUIStyle heading,text,muted,button,input;
  Texture2D panel,paper;
  string Folder {get{return Path.Combine(Application.persistentDataPath,Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-test")>=0?"SmokeTestLessons":"Lessons");}}
  string NotePath {get{return Path.Combine(Folder,(account==null?"legacy":account.id)+"-"+(string.IsNullOrEmpty(lesson.familyId)?SafeName(lesson.title):lesson.id)+".notes.json");}}
  static string SafeName(string s) {return string.Concat(s.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c)).Substring(0,Math.Min(s.Length,80));}
  string Stamp(float seconds) {return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss");}
  LessonSection Current {get{return lesson.sections[section];}}

  void Start() {
   readAloud=gameObject.AddComponent<PersonalReadAloud>();readAloud.Passage=()=>lesson==null?"":Current.narration;readAloud.Notes=()=>notes;readAloud.Answer=()=>answer;
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--study-lab-test")>=0){SceneManager.LoadScene("Classroom");return;}
   Application.runInBackground=true;
   Directory.CreateDirectory(Folder);RefreshCatalog();
   eye=new GameObject("Clarenza Learner").AddComponent<Camera>();eye.tag="MainCamera";eye.nearClipPlane=.08f;eye.farClipPlane=150;
   eye.gameObject.AddComponent<AudioListener>();eye.fieldOfView=65;
   Seat(1);
   codex=new GameObject("Crystal Codex").AddComponent<CrystalCodex>();codex.Initialize(eye);
   professor=new GameObject("Professor visual root").transform;
   professor.position=new Vector3(boardCenter.x-2,boardCenter.y-2.75f,boardCenter.z-1.5f);
   if(teacherPrefab==null)throw new Exception("Teacher asset missing from scene");
   var teacher=Instantiate(teacherPrefab,professor);teacher.transform.localPosition=Vector3.zero;teacher.transform.localRotation=Quaternion.Euler(0,180,0);
   var renderers=teacher.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   teacher.transform.localScale*=1.8f/bounds.size.y;
   bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
   teacher.transform.position+=new Vector3(professor.position.x-bounds.center.x,professor.position.y-bounds.min.y,professor.position.z-bounds.center.z);
   teacherMotion=professor.gameObject.AddComponent<TeacherPresentation>();teacherMotion.Initialize(teacher.transform,boardCenter);
   teacherWalk=professor.gameObject.AddComponent<TeacherWalk>();teacherWalk.Initialize(walkRoutes,teacherMotion);teacherMotion.SetWalk(teacherWalk);
   var voice=new GameObject("Professor voice at head height");voice.transform.SetParent(professor,false);voice.transform.localPosition=Vector3.up*1.55f;speech=voice.AddComponent<AudioSource>();speech.spatialBlend=1;speech.minDistance=4;speech.maxDistance=50;speech.rolloffMode=AudioRolloffMode.Linear;speech.dopplerLevel=0;
   var slate=new GameObject("Teaching surface overlay").transform;slate.position=boardCenter+new Vector3(0,0,-.13f);
   Shape("Readable chalk surface",PrimitiveType.Cube,slate,Vector3.zero,new Vector3(9.8f,3.65f,.03f),new Color(.025f,.1f,.08f));
   var boardObject=new GameObject("Lesson writing");boardObject.transform.position=boardCenter+new Vector3(0,0,-.2f);
   board=boardObject.AddComponent<TextMesh>();board.fontSize=64;board.characterSize=.052f;board.anchor=TextAnchor.MiddleCenter;board.alignment=TextAlignment.Center;board.color=Color.white;
   board.text="CLARENZA\nYour auditorium. Your pace.";
   crowd=new GameObject("Demonstration students - not connected users");
   for(int i=0;i<seatPositions.Length;i+=Math.Max(1,seatPositions.Length/8)) {
    var avatar=new GameObject("Sample student").transform;avatar.parent=crowd.transform;avatar.position=seatPositions[i]-new Vector3(0,.8f,0);
    Shape("Student body",PrimitiveType.Capsule,avatar,new Vector3(0,-.1f,0),new Vector3(.38f,.38f,.32f),new Color(.3f,.38f,.53f));
    Shape("Student head",PrimitiveType.Sphere,avatar,new Vector3(0,.45f,0),Vector3.one*.3f,new Color(.65f,.5f,.4f));
   }
   draft="A complete circuit provides a continuous path for electric current. A battery supplies electrical energy.\n\nA switch opens or closes the path. Opening a switch breaks the circuit and stops current.\n\nA lamp converts electrical energy to light and heat. In this simple series circuit, the battery, lamp and closed switch must all be connected for the lamp to light.";
   StartCoroutine(Post("/health","{}",r=>health=JsonUtility.FromJson<ServiceHealth>(r),false));
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--smoke-test")>=0) StartCoroutine(Smoke());
  }
  GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Color color) {
   var o=GameObject.CreatePrimitive(type);o.name=name;o.transform.SetParent(parent,false);o.transform.localPosition=pos;o.transform.localScale=scale;
   var mat=new Material(presenterMaterial);mat.color=color;o.GetComponent<Renderer>().material=mat;return o;
  }
  void Seat(int level) {
   var ordered=seatPositions.OrderBy(p=>p.y).ToArray();
   if(ordered.Length>0) {
    float y=ordered[Mathf.Clamp((ordered.Length-1)*level/2,0,ordered.Length-1)].y;
    seat=ordered.Where(p=>Math.Abs(p.y-y)<.25f).OrderBy(p=>Math.Abs(p.x-boardCenter.x)).First();
   }else seat=boardCenter+new Vector3(0,0,-8);
   eye.transform.position=seat;eye.transform.LookAt(boardCenter);yaw=eye.transform.eulerAngles.y;pitch=eye.transform.eulerAngles.x;
  }
  void Update() {
   if(screen=="class"&&Time.unscaledTime-progressSavedAt>10){SaveProgress();progressSavedAt=Time.unscaledTime;}
   if(teacherWalk!=null){teacherWalk.lecturePlaying=playing&&screen=="class";teacherWalk.narrationActive=speechLoading||speech.isPlaying;teacherWalk.focusMode=mode==1||boardFocus;}
   if(boardPending&&teacherMotion!=null&&teacherMotion.GestureTime>=.75f&&lesson!=null){board.text=Wrap(Current.title+"\n\n"+Current.board,48);boardPending=false;}
   if(codex!=null){codex.gameObject.SetActive(screen=="class");codex.SetOpen(book);}
   if(readAloud!=null&&readAloud.Open){if(Input.GetKeyDown(KeyCode.Escape)){readAloud.Stop();readAloud.Open=false;}return;}
   if(Input.GetKeyDown(KeyCode.B)&&screen=="class"&&(!book||bookTab==0||bookTab==3)) book=!book;
   if(Input.GetKeyDown(KeyCode.Escape)) {book=false;boardFocus=false;playing=false;speech.Pause();}
   if(screen=="class") {
    if(Input.GetKeyDown(KeyCode.H)&&(!book||bookTab==0)) Mark();
    if(Input.GetMouseButton(1)&&!book&&!boardFocus) {yaw+=Input.GetAxis("Mouse X")*2;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2,-45,60);eye.transform.rotation=Quaternion.Euler(pitch,yaw,0);}
    if(playing) {
     clock+=Time.deltaTime;sectionTime+=Time.deltaTime;
     if(!speechLoading&&!speech.isPlaying&&!teacherMotion.IsPresenting)passageQuietTime+=Time.deltaTime;else passageQuietTime=0;
     if(!walkingTest&&passageQuietTime>=6&&sectionTime>Mathf.Max(12,Current.narration.Split(' ').Length/2.1f)&&!speech.isPlaying&&!busy&&!speechLoading) {if(section<lesson.sections.Length-1) Next(1);else {playing=false;status="Lesson finished. Open your book to review or ask a question.";}}
    }
   }
   if(crowd!=null) crowd.SetActive(mode==0);
  }
  void RefreshCatalog() {if(account!=null)StartCoroutine(Post("/catalog/list","{}",r=>sharedCatalog=JsonUtility.FromJson<CatalogResult>(r).items));}
  void SaveLesson() {LessonImport.Validate(lesson);File.WriteAllText(Path.Combine(Folder,SafeName(lesson.title)+".lesson.json"),JsonUtility.ToJson(lesson,true));RefreshCatalog();}
  void LoadLesson(string path) {try{var loaded=JsonUtility.FromJson<LessonDocument>(File.ReadAllText(path));LessonImport.Validate(loaded);lesson=loaded;section=0;screen="preview";scroll=Vector2.zero;status="Review the lesson before entering class.";}catch(Exception e){status=e.Message;}}
  void Preview() {try{readOnlyLesson=false;lesson=LessonImport.Local(draft,string.IsNullOrWhiteSpace(subject)?"My lesson":subject);section=0;screen="preview";scroll=Vector2.zero;status="Source playback preview. AI has not rewritten this material.";}catch(Exception e){status=e.Message;}}
  void Enter() {
   if(IsSmoke){try{SaveLesson();}catch(Exception e){status=e.Message;return;}EnterReady();return;}
   if(account==null){status="Sign in first.";return;}
   if(readOnlyLesson){EnterReady();return;}
   if(!studyMode&&!CanTeach){status="Students can upload only in private study mode.";return;}
   try{LessonImport.Validate(lesson);}catch(Exception e){status=e.Message;return;}
   StartCoroutine(Post("/catalog/save",JsonUtility.ToJson(new SaveRequest{scope=studyMode?"study":"published",parentId=lesson.id,reviewed=!studyMode,document=lesson}),r=>{lesson=JsonUtility.FromJson<SaveResult>(r).document;RefreshCatalog();EnterReady();}));
  }
  void EnterReady() {
   screen="class";book=false;section=0;clock=sectionTime=0;transcript.Clear();transcriptTimes.Clear();marking=false;score=quizIndex=0;selected=-1;submitted=false;answer="";
   notes="";try{if(File.Exists(NotePath))notes=JsonUtility.FromJson<LearningNotes>(File.ReadAllText(NotePath)).text;}catch(Exception){status="Previous notes could not be loaded.";}
   RestoreProgress();SetSection();playing=false;status="Ready. Play lecture for narration, or advance passages manually.";
  }
  string ProgressPath {get{return Path.Combine(Folder,(account==null?"legacy":account.id)+"-"+lesson.id+".progress.json");}}
  void SaveProgress(){
   if(lesson==null||account==null||string.IsNullOrEmpty(lesson.id))return;
   try{Directory.CreateDirectory(Folder);File.WriteAllText(ProgressPath,JsonUtility.ToJson(new LessonProgress{section=section,quizIndex=quizIndex,selected=selected,score=score,submitted=submitted,clock=clock}));}catch(Exception e){status="Progress could not be saved: "+e.Message;}
  }
  void RestoreProgress(){
   if(account==null||string.IsNullOrEmpty(lesson.id))return;
   try{if(File.Exists(ProgressPath)){var p=JsonUtility.FromJson<LessonProgress>(File.ReadAllText(ProgressPath));section=Mathf.Clamp(p.section,0,lesson.sections.Length-1);quizIndex=Mathf.Clamp(p.quizIndex,0,(lesson.quiz??new LessonQuestion[0]).Length);selected=Mathf.Clamp(p.selected,-1,2);score=Mathf.Clamp(p.score,0,quizIndex+1);submitted=p.submitted;clock=Mathf.Max(0,p.clock);}}catch(Exception){status="Saved progress could not be read.";}
  }
  void OnApplicationQuit(){SaveProgress();}
  void SetSection() {passageQuietTime=0;if(teacherMotion!=null)teacherMotion.Present();sectionTime=0;boardPending=true;transcript.Add(Current.narration);transcriptTimes.Add(clock);}
  void Next(int direction) {speechVersion++;speechLoading=false;speech.Stop();section=Mathf.Clamp(section+direction,0,lesson.sections.Length-1);SetSection();SaveProgress();if(playing) Speak();}
  void Speak() {
   if(!health.speechReady){status="Local narration service is offline. You can still read and advance lesson text.";return;}
   int version=++speechVersion;speechLoading=true;
   StartCoroutine(SpeechJob(version));
  }
  IEnumerator SpeechJob(int version) {
   bool loaded=false;
   yield return Post("/speech",JsonUtility.ToJson(new SpeechRequest{text=Current.narration}),r=>{
    if(version!=speechVersion||screen!="class")return;
    var clip=LessonImport.Wav(Convert.FromBase64String(JsonUtility.FromJson<SpeechResponse>(r).wav));
    if(speech.clip!=null)Destroy(speech.clip);speech.clip=clip;loaded=true;sectionTime=0;
   },false);
   if(version!=speechVersion)yield break;
   if(!loaded){speechLoading=false;yield break;}
   while(version==speechVersion&&screen=="class"&&(!playing||teacherMotion.IsPresenting||!teacherWalk.ReadyToTeach))yield return null;
   if(version!=speechVersion||screen!="class")yield break;
   if(speech.clip!=null){speech.Play();sectionTime=0;}
   speechLoading=false;
  }
  static string Wrap(string s,int width) {
   var result=new StringBuilder();int line=0;
   foreach(var word in s.Split(' ')){if(line+word.Length>width){result.Append('\n');line=0;}result.Append(word+" ");line+=word.Length+1;if(word.Contains("\n"))line=word.Substring(word.LastIndexOf('\n')+1).Length;}
   return result.ToString();
  }
  void Mark() {
   if(!marking){markStart=Mathf.Max(0,clock-lookback);marking=true;status="Highlight started, including the preceding "+lookback+" seconds.";}
   else {
    var passages=transcript.Where((s,i)=>transcriptTimes[i]<=clock&&(i==transcriptTimes.Count-1||transcriptTimes[i+1]>=markStart));
    notes+="\n["+Stamp(markStart)+"–"+Stamp(clock)+"] "+string.Join("\n",passages)+"\n";
    marking=false;SaveNotes();status="Highlight saved to your book (passage-level timing).";
   }
  }
  void SaveNotes(){try{File.WriteAllText(NotePath,JsonUtility.ToJson(new LearningNotes{text=notes},true));status="Notes saved on this computer.";}catch(Exception e){status="Could not save notes: "+e.Message;}}
  IEnumerator Post(string route,string json,Action<string> success,bool showBusy=true) {
   if(showBusy)busy=true;
   using(var req=new UnityWebRequest("http://127.0.0.1:47831"+route,"POST")) {
    req.uploadHandler=new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));req.downloadHandler=new DownloadHandlerBuffer();req.SetRequestHeader("Content-Type","application/json");if(account!=null)req.SetRequestHeader("Authorization","Bearer "+account.token);req.timeout=110;
    yield return req.SendWebRequest();
    if(req.isNetworkError||req.isHttpError){var message="Local service is unavailable. Start the Clarenza launcher to enable narration and AI.";try{var error=JsonUtility.FromJson<ServiceError>(req.downloadHandler.text);if(!string.IsNullOrEmpty(error.error))message=error.error;}catch{}status=message;}
    else try{success(req.downloadHandler.text);}catch(Exception e){status="Could not use the response: "+e.Message;}
   }
   if(showBusy)busy=false;
  }
  Texture2D ColorTexture(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}
  void Styles(){if(heading!=null)return;homeBackground=ColorTexture(new Color(.035f,.047f,.065f,1));homeAccent=ColorTexture(new Color(.66f,.84f,.85f,1));panel=ColorTexture(new Color(.025f,.045f,.07f,.96f));paper=ColorTexture(new Color(.07f,.12f,.16f,.98f));
   heading=new GUIStyle(GUI.skin.label){fontSize=27,fontStyle=FontStyle.Bold,wordWrap=true};text=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};muted=new GUIStyle(text){fontSize=14};button=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true,padding=new RectOffset(12,12,10,10)};input=new GUIStyle(GUI.skin.textArea){fontSize=17,wordWrap=true};
   button.normal.background=ColorTexture(new Color(.075f,.2f,.27f));button.hover.background=ColorTexture(new Color(.11f,.34f,.42f));button.active.background=button.hover.background;button.normal.textColor=Color.white;button.hover.textColor=Color.white;
   heading.normal.textColor=new Color(.82f,.94f,1);text.normal.textColor=new Color(.9f,.95f,.98f);muted.normal.textColor=new Color(.62f,.77f,.84f);
  }
  bool Btn(string s){return GUILayout.Button(s,button,GUILayout.MinHeight(40));}
  void OnGUI(){Styles();float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);GUI.matrix=Matrix4x4.TRS(Vector3.zero,Quaternion.identity,new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
   if(account==null&&!IsSmoke){LoginUI();return;}
   if(screen=="lobby")GUI.DrawTexture(new Rect(0,0,w,h),homeBackground);
   if(screen=="class"&&mode==1){GUI.color=new Color(0,0,0,.72f);GUI.DrawTexture(new Rect(0,0,w*.19f,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(w*.81f,0,w*.19f,h),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,0,w,h*.16f),Texture2D.whiteTexture);GUI.color=Color.white;}
   GUI.DrawTexture(new Rect(18,16,w-36,68),panel);GUI.Label(new Rect(36,30,650,45),"CLARENZA / "+(screen=="class"?lesson.title:screen=="preview"?"Review lesson":"Your learning space"),heading);
   GUI.Label(new Rect(w-395,38,360,32),(account==null?"LOCAL PILOT":account.username+" / "+account.role.ToUpper())+" • "+(health.aiReady?"AI connected":"AI setup required"),muted);
   if(readAloud!=null&&readAloud.Open){readAloud.Draw(w,h,panel,heading,text,input,button);return;}
   if(screen!="class"){
    if(screen=="lobby"){
     LobbyNavigation(w,h);
     if(lobbyPage=="home"){HomeUI(w,h);GUI.DrawTexture(new Rect(290,h-58,w-310,46),panel);GUI.Label(new Rect(306,h-49,w-342,42),busy?"Working…":status,muted);return;}
    }
    float contentX=screen=="lobby"?290:28;float contentWidth=screen=="lobby"?w-310:Mathf.Min(950,w-56);
    GUI.DrawTexture(new Rect(contentX,104,contentWidth,h-178),panel);GUILayout.BeginArea(new Rect(contentX+24,126,contentWidth-48,h-225));scroll=GUILayout.BeginScrollView(scroll);
    GUI.enabled=!busy;
    if(screen=="lobby") Lobby();else PreviewUI();
    GUI.enabled=true;GUILayout.EndScrollView();GUILayout.EndArea();
   }else{
    GUI.DrawTexture(new Rect(20,h-172,w-40,104),panel);GUILayout.BeginArea(new Rect(38,h-164,w-76,90));
    GUILayout.BeginHorizontal();if(Btn("‹ Previous"))Next(-1);if(Btn(playing?"Pause lecture":"Play lecture")){playing=!playing;if(playing){if(speech.clip!=null&&speech.time>0)speech.UnPause();else if(!speechLoading)Speak();}else {speech.Pause();SaveProgress();}}if(Btn("Next ›"))Next(1);if(Btn(book?"Close book [B]":"Learning book [B]"))book=!book;if(Btn("Board focus"))boardFocus=!boardFocus;if(Btn(teacherWalk.enabledByUser?"Teacher: roaming":"Teacher: stationary"))teacherWalk.enabledByUser=!teacherWalk.enabledByUser;GUILayout.EndHorizontal();
    GUILayout.BeginHorizontal();GUILayout.Label("Passage "+(section+1)+" / "+lesson.sections.Length+"  •  "+Stamp(clock),muted);if(Btn("Front"))Seat(0);if(Btn("Middle"))Seat(1);if(Btn("Back"))Seat(2);if(Btn(mode==0?"Focus mode":"Social view"))mode=1-mode;if(Btn("Lobby")){SaveProgress();SaveNotes();speechVersion++;speech.Stop();playing=false;screen="lobby";NavigateLobby("home");book=false;boardFocus=false;}GUILayout.EndHorizontal();GUILayout.EndArea();
    if(!book&&!boardFocus){GUI.DrawTexture(new Rect(32,100,440,92),panel);GUI.Label(new Rect(48,110,406,80),"Hold right mouse to look around.\n[B] Book · [H] Start/stop highlight\nStudent figures are local placeholders.",muted);}
    if(boardFocus){GUI.DrawTexture(new Rect(w*.2f,110,w*.6f,h-300),panel);GUI.Label(new Rect(w*.23f,140,w*.54f,60),Current.title,heading);GUI.Label(new Rect(w*.23f,220,w*.54f,h-410),Current.board,new GUIStyle(text){fontSize=28});}
    if(book)BookUI(w,h);
   }
   GUI.DrawTexture(new Rect(20,h-58,w-40,46),panel);GUI.Label(new Rect(34,h-49,w-68,42),busy?"Working on your request…":status,muted);
  }
  void OpenReadAloud(){
   playing=false;speechVersion++;speechLoading=false;speech.Pause();SaveProgress();
   readAloud.ExportDirectory=Path.Combine(Folder,"AudioExports",account==null?"smoke":account.id);readAloud.Open=true;
  }
  System.Diagnostics.Process browserProcess;
  void OpenBrowser(){
   playing=false;if(speech!=null)speech.Pause();
   try{
    if(browserProcess!=null&&!browserProcess.HasExited){status="The browser is already open. Switch to Clarenza Browser in the taskbar.";return;}
    string exe=Path.GetFullPath(Path.Combine(Application.dataPath,"../Browser/ClarenzaBrowser.exe"));
    if(!File.Exists(exe))throw new Exception("Browser is available in the Windows build. Run Browser/Build.ps1 if it is missing.");
    browserProcess=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe){UseShellExecute=false});status="Browser opened. Close it to return; resume the lecture when ready.";
   }catch(Exception e){status=e.Message;}
  }
  void LoginUI(){
   GUI.DrawTexture(new Rect(40,40,650,520),panel);GUILayout.BeginArea(new Rect(66,66,598,470));
   GUILayout.Label("CLARENZA / Sign in",heading);GUILayout.Label("Your account determines whether you teach or study.",text);
   loginName=GUILayout.TextField(loginName,input,GUILayout.Height(42));GUILayout.Label("Username",muted);
   loginPassword=GUILayout.PasswordField(loginPassword,'*',128,input,GUILayout.Height(42));GUILayout.Label("Password",muted);
   GUI.enabled=!busy;
   if(Btn("Sign in"))StartCoroutine(Post("/auth/login",JsonUtility.ToJson(new Credentials{username=loginName,password=loginPassword}),r=>{readAloud.ResetPrivate();account=JsonUtility.FromJson<Account>(r);loginPassword="";screen="lobby";lobbyPage="home";studyMode=false;readOnlyLesson=false;RefreshCatalog();status="Welcome, "+account.username;}));
   GUI.enabled=true;GUILayout.Label("First setup: the computer owner runs Create-Account.cmd to create an administrator. Administrators can then create teacher and student accounts here.",muted);
   GUILayout.Label(status,text);GUILayout.EndArea();
  }
  void NavigateLobby(string page){lobbyPage=page;scroll=Vector2.zero;status="";}
  void LobbyNavigation(float w,float h){
   GUI.DrawTexture(new Rect(20,104,248,h-178),panel);GUILayout.BeginArea(new Rect(38,126,212,h-222));
   GUILayout.Label("YOUR CODEX",muted);GUILayout.Space(20);GUI.enabled=!busy;
   if(Btn((lobbyPage=="home"?"• ":"")+"Home"))NavigateLobby("home");
   if(Btn("Teacher classes")){libraryTab=0;NavigateLobby("catalog");}
   if(Btn("My library")){libraryTab=1;NavigateLobby("catalog");}
   GUILayout.Space(16);
   if(Btn("Create an AI class")){studyMode=true;NavigateLobby("create");}
   if(CanTeach&&Btn("Teacher studio")){studyMode=false;NavigateLobby("create");}
   if(Btn("Study tools"))NavigateLobby("tools");
   GUILayout.FlexibleSpace();
   GUILayout.Label(account==null?"Desktop preview":account.username, text);GUILayout.Label(account==null?"Local learning space":account.role.ToUpperInvariant(),muted);
   if(Btn("Account & settings"))NavigateLobby("account");GUI.enabled=true;GUILayout.EndArea();
  }
  void HomeUI(float w,float h){
   float x=290,width=w-310;GUI.enabled=!busy;
   GUI.DrawTexture(new Rect(x,104,width,238),paper);GUI.DrawTexture(new Rect(x,104,4,238),homeAccent);
   GUI.Label(new Rect(x+30,126,width-60,28),"LEARN AT YOUR OWN PACE",muted);
   GUI.Label(new Rect(x+30,164,width-270,70),"What will you learn today?",new GUIStyle(heading){fontSize=38});
   GUI.Label(new Rect(x+30,245,width-290,64),"Take a teacher’s class, explore a topic with AI, or pick up your personal study.",text);
   // A restrained Codex motif, drawn with interface primitives.
   GUI.DrawTexture(new Rect(x+width-194,152,118,146),panel);GUI.DrawTexture(new Rect(x+width-194,152,5,146),homeAccent);
   GUI.DrawTexture(new Rect(x+width-167,184,64,2),homeAccent);GUI.DrawTexture(new Rect(x+width-167,201,47,2),homeAccent);GUI.DrawTexture(new Rect(x+width-145,238,18,18),homeAccent);
   float gap=18,cardWidth=(width-gap)/2;
   GUI.DrawTexture(new Rect(x,362,cardWidth,205),panel);GUILayout.BeginArea(new Rect(x+24,382,cardWidth-48,166));GUILayout.Label("Learn with a teacher",heading);GUILayout.Label("Browse published lessons in your auditorium.",text);GUILayout.FlexibleSpace();if(Btn("Explore classes →")){libraryTab=0;NavigateLobby("catalog");}GUILayout.EndArea();
   GUI.DrawTexture(new Rect(x+cardWidth+gap,362,cardWidth,205),panel);GUILayout.BeginArea(new Rect(x+cardWidth+gap+24,382,cardWidth-48,166));GUILayout.Label("Create your own class",heading);GUILayout.Label("Choose a topic and build a private AI study lesson.",text);GUILayout.FlexibleSpace();if(Btn("Start a private class →")){studyMode=true;NavigateLobby("create");}GUILayout.EndArea();
   GUI.DrawTexture(new Rect(x,587,width,h-665),panel);GUILayout.BeginArea(new Rect(x+24,604,width-48,h-707));GUILayout.Label("Your study essentials",heading);GUILayout.Space(8);GUILayout.BeginHorizontal();if(Btn("My library")){libraryTab=1;NavigateLobby("catalog");}if(Btn("Read aloud"))OpenReadAloud();if(Btn("Circuit lab"))SceneManager.LoadScene("Classroom");GUILayout.EndHorizontal();GUILayout.Space(8);GUILayout.Label("Your notes, lessons and progress stay on this computer.",muted);GUILayout.EndArea();
   GUI.enabled=true;
  }
  void Lobby(){
   if(lobbyPage=="create"){
    GUILayout.Label(studyMode?"Create a private class":"Teacher studio",heading);GUILayout.Label(studyMode?"Start with a topic or your own material. Your lesson stays private.":"Import your material, review the lesson, then approve it for your students.",text);GUILayout.Space(16);
    if(!(CanTeach||studyMode||IsSmoke)){NavigateLobby("home");return;}
   subject=GUILayout.TextField(subject,input,GUILayout.Height(38));GUILayout.Label("Topic / lesson title",muted);
   requestedLevel=GUILayout.Toolbar(requestedLevel,new[]{"Beginner","Intermediate","Advanced"},button);
   GUILayout.Label("Target duration: "+requestedMinutes+" minutes (approximate)",muted);requestedMinutes=Mathf.RoundToInt(GUILayout.HorizontalSlider(requestedMinutes,5,30));
   requestedLanguage=GUILayout.TextField(requestedLanguage,input);GUILayout.Label("Teaching language",muted);
   filePath=GUILayout.TextField(filePath,input,GUILayout.Height(38));GUILayout.Label("Full path to .txt, .md or .pptx",muted);
   if(Btn("Import file")){try{draft=LessonImport.Read(filePath.Trim().Trim('"'));LessonImport.Sources(draft);subject=Path.GetFileNameWithoutExtension(filePath);status="Text imported. PowerPoint images, animations and speaker notes are not imported.";}catch(Exception e){status=e.Message;}}
   GUILayout.Label("Lesson text — paste or edit below",text);draft=GUILayout.TextArea(draft,input,GUILayout.Height(170));
   if(Btn("Preview source lesson (offline)"))Preview();
   if(Btn("Create AI class: slides + narration + quiz")){
    try{var src=string.IsNullOrWhiteSpace(draft)?new string[0]:LessonImport.Sources(draft);StartCoroutine(Post("/class/request",JsonUtility.ToJson(new ClassRequest{sources=src,topic=subject,level=new[]{"Beginner","Intermediate","Advanced"}[requestedLevel],minutes=requestedMinutes,language=requestedLanguage}),r=>{lesson=JsonUtility.FromJson<LessonDocument>(r);LessonImport.Validate(lesson);readOnlyLesson=false;section=0;screen="preview";scroll=Vector2.zero;status="AI draft created. Review sources, slides and quiz before use.";}));}catch(Exception e){status=e.Message;}
   }
   GUILayout.Label("Without pasted material, AI uses a matching teacher-approved lesson from this local catalog. It does not search the web.",muted);
   GUILayout.Label("AI generation sends the lesson text to OpenAI through your local service.",muted);
   }

   if(lobbyPage=="catalog"){
   GUILayout.Space(16);GUILayout.Label(libraryTab==0?"Teacher classes":"My library",heading);GUILayout.Label(libraryTab==0?"Choose a published class and take a seat.":"Your private study lessons and unpublished drafts.",text);GUILayout.Label("Search by title",muted);search=GUILayout.TextField(search,input,GUILayout.Height(34));
   foreach(var item in sharedCatalog.Where(x=>(libraryTab==0?x.scope=="published":x.scope!="published")&&x.title.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)){
    GUILayout.BeginHorizontal();
    if(Btn((item.scope=="study"?(item.classType=="ai"?"[Private AI] ":"[Private study] "):item.approval=="teacher-approved"?"[Teacher-approved] ":item.scope=="draft"?"[Draft] ":"[Legacy class] ")+item.title+" • v"+Mathf.Max(1,item.version))){var id=item.id;StartCoroutine(Post("/catalog/get",JsonUtility.ToJson(new IdRequest{id=id}),r=>{lesson=JsonUtility.FromJson<LessonDocument>(r);LessonImport.Validate(lesson);readOnlyLesson=true;EnterReady();}));}
    if(account!=null&&item.ownerId==account.id&&Btn("Revise")){var id=item.id;var privateLesson=item.scope=="study";StartCoroutine(Post("/catalog/get",JsonUtility.ToJson(new IdRequest{id=id}),r=>{lesson=JsonUtility.FromJson<LessonDocument>(r);lesson.approval="draft";lesson.approvedBy="";lesson.approvedAt="";studyMode=privateLesson;readOnlyLesson=false;section=0;screen="preview";status="Editing a new version. The previous published lesson stays unchanged.";}));}
    GUILayout.EndHorizontal();
   }
    if(!sharedCatalog.Any(x=>(libraryTab==0?x.scope=="published":x.scope!="published")&&x.title.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0))GUILayout.Label(string.IsNullOrEmpty(search)?(libraryTab==0?"No published classes yet. A teacher can add one from Teacher studio.":"Your library is empty. Create a private class to get started."):"No matching lessons. Try another title.",text);
   }
   if(lobbyPage=="tools"){
    GUILayout.Label("Study tools",heading);GUILayout.Label("Listen, practice or browse while your lesson is paused.",text);GUILayout.Space(20);
    if(Btn("Read aloud / Export audio"))OpenReadAloud();
    if(Btn("Open circuit study room"))SceneManager.LoadScene("Classroom");
    if(Btn("Browser / YouTube"))OpenBrowser();
   }
   if(lobbyPage=="account"){
    GUILayout.Label("Account & settings",heading);GUILayout.Label(account==null?"Local preview":account.username+" / "+account.role,text);
    if(Btn("Reconnect local service"))StartCoroutine(Post("/health","{}",r=>{health=JsonUtility.FromJson<ServiceHealth>(r);status="Local service connected.";}));
    GUILayout.Label(health.aiReady?"AI provider is configured.":"AI generation needs local setup with Launch-With-AI.cmd. Imported lessons and read-aloud work without a paid AI key.",text);
   if(account!=null&&account.role=="admin"){
    GUILayout.Label("Admin: create an account",heading);newName=GUILayout.TextField(newName,input);GUILayout.Label("Username",muted);newPassword=GUILayout.PasswordField(newPassword,'*',128,input);GUILayout.Label("Password: at least 12 characters",muted);
    newRole=GUILayout.Toolbar(newRole,new[]{"Admin","Teacher","Student"},button);
    if(Btn("Create account"))StartCoroutine(Post("/accounts/create",JsonUtility.ToJson(new Credentials{username=newName,password=newPassword,role=new[]{"admin","teacher","student"}[newRole]}),r=>{newPassword="";status="Account created.";}));
   }
    if(account!=null&&Btn("Sign out")){readAloud.ResetPrivate();SaveProgress();StartCoroutine(Post("/auth/logout","{}",r=>{account=null;sharedCatalog=new CatalogItem[0];lesson=null;draft=notes=answer=question="";loginPassword="";studyMode=false;readOnlyLesson=false;lobbyPage="home";status="Signed out.";}));}
   }
  }
  void PreviewUI(){GUILayout.Label("Review your lesson",heading);GUILayout.Label(lesson.mode,muted);GUILayout.Label("Draft — not approved. "+lesson.sourceOrigin,muted);lesson.title=GUILayout.TextField(lesson.title,input,GUILayout.Height(40));
   GUILayout.BeginHorizontal();if(Btn("‹"))section=Mathf.Max(0,section-1);GUILayout.Label((section+1)+" / "+lesson.sections.Length,text);if(Btn("›"))section=Mathf.Min(lesson.sections.Length-1,section+1);GUILayout.EndHorizontal();
   Current.title=GUILayout.TextField(Current.title,input,GUILayout.Height(38));GUILayout.Label("Professor narration",text);Current.narration=GUILayout.TextArea(Current.narration,input,GUILayout.Height(150));GUILayout.Label("Board content",text);Current.board=GUILayout.TextArea(Current.board,input,GUILayout.Height(120));
   GUILayout.Label("Source passages: "+string.Join(", ",Current.sourceIds.Select(n=>n.ToString())),muted);
   foreach(int id in Current.sourceIds)GUILayout.Label("["+id+"] "+lesson.sources[id-1],muted);
   GUILayout.Label("Review quiz answers and their source support",text);
   foreach(var q in lesson.quiz??new LessonQuestion[0]){GUILayout.Label(q.question,text);for(int i=0;i<q.options.Length;i++)q.options[i]=GUILayout.TextField(q.options[i],input);q.correct=GUILayout.Toolbar(q.correct,new[]{"Answer 1","Answer 2","Answer 3"},button);q.explanation=GUILayout.TextArea(q.explanation,input);GUILayout.Label("Sources: "+string.Join(", ",q.sourceIds.Select(n=>n.ToString())),muted);}
   if(CanTeach&&!studyMode&&Btn("Save unpublished draft")){try{LessonImport.Validate(lesson);StartCoroutine(Post("/catalog/save",JsonUtility.ToJson(new SaveRequest{scope="draft",parentId=lesson.id,document=lesson}),r=>{lesson=JsonUtility.FromJson<SaveResult>(r).document;RefreshCatalog();screen="lobby";libraryTab=1;NavigateLobby("catalog");status="Draft saved. Students cannot see it.";}));}catch(Exception e){status=e.Message;}}
   if(Btn(studyMode?"Save private study and enter":"Approve reviewed lesson, publish and enter"))Enter();if(Btn("Back to import")){screen="lobby";NavigateLobby("create");}
  }
  void BookUI(float w,float h){
   if(codex==null||!codex.Ready)return;
   float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
   GUILayout.BeginArea(codex.PageRect(true,scale));
   if(Btn("Read aloud / Export audio"))OpenReadAloud();
   GUILayout.Label("CRYSTAL CODEX",heading);GUILayout.Label("Your personal learning companion",muted);GUILayout.Space(16);GUILayout.Label(lesson.title,text);GUILayout.Label(lesson.approval=="teacher-approved"?"Teacher-approved by "+lesson.approvedBy+" • v"+lesson.version:"Private / unreviewed learning material",muted);GUILayout.Space(18);
   string[] tabs={"Transcript","Notes","AI tutor","Practice"};
   for(int i=0;i<tabs.Length;i++)if(Btn((bookTab==i?"◆  ":"◇  ")+tabs[i])){bookTab=i;transcriptScroll=Vector2.zero;}
   GUILayout.Space(12);if(Btn("Browser / YouTube"))OpenBrowser();
   if(Btn("Read aloud / Export audio"))OpenReadAloud();
   if(Btn("Close Codex"))book=false;
   GUILayout.Label("Private notes · Sources with answers",muted);GUILayout.EndArea();
   GUILayout.BeginArea(codex.PageRect(false,scale));GUILayout.Label(tabs[bookTab],heading);transcriptScroll=GUILayout.BeginScrollView(transcriptScroll);
   if(bookTab==0){GUILayout.Label("Lesson script • passage-level timestamps",muted);lookback=Mathf.RoundToInt(GUILayout.HorizontalSlider(lookback,0,30));if(Btn(marking?"Stop highlight [H]":"Highlight previous "+lookback+" sec + onward [H]"))Mark();for(int i=0;i<transcript.Count;i++)GUILayout.Label(Stamp(transcriptTimes[i])+"  "+transcript[i],text);}
   if(bookTab==1){GUILayout.Label("Private local notes for this lesson",muted);notes=GUILayout.TextArea(notes,input,GUILayout.MinHeight(240));if(Btn("Save notes"))SaveNotes();}
   if(bookTab==2){GUILayout.Label("Ask about the imported class material",text);question=GUILayout.TextArea(question,input,GUILayout.Height(85));int choice=Array.IndexOf(new[]{"Step by step","Concise","Worked example"},style);style=new[]{"Step by step","Concise","Worked example"}[GUILayout.Toolbar(Mathf.Max(0,choice),new[]{"Step by step","Concise","Example"},button)];GUI.enabled=!busy&&!string.IsNullOrWhiteSpace(question);if(Btn("Ask my AI tutor")){playing=false;speech.Pause();SaveProgress();var requested=question;StartCoroutine(Post("/ask",JsonUtility.ToJson(new TutorRequest{sources=lesson.sources,question=requested,style=style}),r=>{var response=JsonUtility.FromJson<TutorAnswer>(r);if(response.sourceIds==null||response.sourceIds.Any(id=>id<1||id>lesson.sources.Length))throw new Exception("Invalid answer citations.");answer=response.answer+"\n\n"+string.Join("\n\n",response.sourceIds.Select(id=>"["+id+"] "+lesson.sources[id-1]));}));}GUI.enabled=true;GUILayout.Label("Your question and class sources are sent to OpenAI. Personal notes are not included.",muted);GUILayout.Label(answer,text);if(!string.IsNullOrEmpty(answer)&&Btn("Save answer to notes")){notes+="\n"+answer+"\n";SaveNotes();}}
   if(bookTab==3){var quiz=lesson.quiz??new LessonQuestion[0];if(quiz.Length==0)GUILayout.Label("This source-only lesson has no generated quiz. Use AI generation in the lobby to create one.",text);else if(quizIndex>=quiz.Length){GUILayout.Label("Practice complete: "+score+" / "+quiz.Length,text);if(Btn("Try again")){quizIndex=score=0;selected=-1;submitted=false;}}else{var q=quiz[quizIndex];GUILayout.Label(q.question,text);for(int i=0;i<q.options.Length;i++){GUI.enabled=!submitted;if(Btn((selected==i?"● ":"○ ")+q.options[i]))selected=i;}GUI.enabled=true;if(submitted){GUILayout.Label((selected==q.correct?"Correct. ":"Review this: ")+q.explanation,text);if(Btn("Next question")){quizIndex++;selected=-1;submitted=false;SaveProgress();}}else{GUI.enabled=selected>=0;if(Btn("Check answer")){submitted=true;if(selected==q.correct)score++;SaveProgress();}GUI.enabled=true;}}}
   GUILayout.EndScrollView();GUILayout.EndArea();
  }
  IEnumerator ReadAloudSmoke(){
   OpenReadAloud();if(playing||speech.isPlaying)throw new Exception("Read-aloud did not pause lecture");
   readAloud.SetText("");readAloud.Generate();if(readAloud.Loading)throw new Exception("Empty speech accepted");
   readAloud.SetText(new string('a',5001));readAloud.Generate();if(readAloud.Loading)throw new Exception("Oversized speech accepted");
   readAloud.SetText("This cancelled request must never start playing.");readAloud.Generate();readAloud.Stop();yield return new WaitForSeconds(1);
   if(readAloud.IsPlaying||readAloud.HasAudio||readAloud.Loading)throw new Exception("Cancelled speech started");
   readAloud.SetText(readAloud.Passage()+" Read aloud keeps this audio private. You can pause it, resume it, or export a wave file.");readAloud.Rate=-2;readAloud.Generate();
   float deadline=Time.time+75;while(readAloud.Loading&&Time.time<deadline)yield return null;
   if(!readAloud.HasAudio||!readAloud.IsPlaying)throw new Exception("Read-aloud synthesis failed: "+readAloud.Message);
   readAloud.TogglePause();yield return new WaitForSeconds(.3f);if(!readAloud.Paused||readAloud.IsPlaying)throw new Exception("Read-aloud pause failed");
   readAloud.TogglePause();yield return new WaitForSeconds(.3f);if(!readAloud.IsPlaying)throw new Exception("Read-aloud resume failed");
   string first=readAloud.Export(),second=readAloud.Export();if(first==null||second==null||first==second)throw new Exception("Read-aloud export failed");
   var clip=LessonImport.Wav(File.ReadAllBytes(first));if(clip.samples<1)throw new Exception("Exported WAV empty");Destroy(clip);
   var dir=readAloud.ExportDirectory;readAloud.ExportDirectory=first;if(readAloud.Export()!=null)throw new Exception("Invalid export directory accepted");readAloud.ExportDirectory=dir;
   readAloud.Stop();readAloud.SetText(readAloud.Notes());if(readAloud.Text!=notes||readAloud.HasAudio)throw new Exception("Notes load retained stale audio");
   readAloud.SetText("Test tutor answer");answer="Test tutor answer";readAloud.SetText(readAloud.Answer());if(readAloud.Text!=answer)throw new Exception("Tutor answer load failed");
   var endpoint=readAloud.Endpoint;readAloud.Endpoint="http://127.0.0.1:1/speech";readAloud.Generate();deadline=Time.time+80;while(readAloud.Loading&&Time.time<deadline)yield return null;
   if(readAloud.Loading||readAloud.HasAudio)throw new Exception("Unavailable service handling failed");readAloud.Endpoint=endpoint;
   readAloud.SetText("Personal text to clear on account switch");readAloud.ResetPrivate();if(readAloud.Text!=""||readAloud.HasAudio||readAloud.Open||readAloud.ExportDirectory!=null)throw new Exception("Private speech reset failed");
   OpenReadAloud();readAloud.SetText("Read aloud turns your lesson passages, personal notes or tutor answers into local audio. The professor stays paused while you listen.");
   ScreenCapture.CaptureScreenshot(Path.Combine(Path.GetDirectoryName(Application.dataPath),"Evidence","read-aloud.png"));yield return new WaitForSeconds(1);
   readAloud.ResetPrivate();Debug.Log("READ_ALOUD_RUNTIME_TEST_PASSED export="+first);
  }
  IEnumerator ClassContractSmoke(){
   yield return Post("/auth/login",JsonUtility.ToJson(new Credentials{username="test.teacher",password="isolated-test-password"}),r=>account=JsonUtility.FromJson<Account>(r));
   if(account==null)throw new Exception("Isolated class test login failed");
   var document=LessonImport.Local("A closed switch completes a circuit.\n\nAn open switch breaks the circuit.","Class contract circuit");
   LessonDocument approved=null;
   yield return Post("/catalog/save",JsonUtility.ToJson(new SaveRequest{scope="published",reviewed=true,document=document}),r=>approved=JsonUtility.FromJson<SaveResult>(r).document);
   if(approved==null||approved.version!=1||approved.approval!="teacher-approved"||string.IsNullOrEmpty(approved.id))throw new Exception("Unity lesson identity contract failed");
   yield return Post("/auth/login",JsonUtility.ToJson(new Credentials{username="test.student",password="isolated-test-password"}),r=>account=JsonUtility.FromJson<Account>(r));
   lesson=null;yield return Post("/catalog/get",JsonUtility.ToJson(new IdRequest{id=approved.id}),r=>lesson=JsonUtility.FromJson<LessonDocument>(r));
   if(lesson==null||lesson.id!=approved.id||lesson.approvedBy!="test.teacher")throw new Exception("Student teacher-class access failed");
   EnterReady();section=1;clock=24;SaveProgress();section=0;clock=0;EnterReady();
   if(section!=1||clock!=24)throw new Exception("Progress resume failed");
   Debug.Log("TWO_CLASS_UNITY_CONTRACT_AND_RESUME_PASSED");
   account=null;lesson=null;screen="lobby";
  }
  IEnumerator Smoke(){
   yield return new WaitForSeconds(2);
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--class-test")>=0)yield return ClassContractSmoke();
   string dir=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Evidence");Directory.CreateDirectory(dir);
   ScreenCapture.CaptureScreenshot(Path.Combine(dir,"lobby.png"));yield return new WaitForSeconds(1);
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--home-test")>=0){
    studyMode=true;NavigateLobby("create");yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"home-create.png"));yield return new WaitForSeconds(.5f);
    libraryTab=1;NavigateLobby("catalog");yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"home-library.png"));yield return new WaitForSeconds(.5f);
    NavigateLobby("tools");yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"home-tools.png"));yield return new WaitForSeconds(.5f);
    NavigateLobby("home");studyMode=false;
   }
   Preview();Enter();status="Original auditorium • local desktop pilot";yield return new WaitForSeconds(2);
   ScreenCapture.CaptureScreenshot(Path.Combine(dir,"auditorium.png"));yield return new WaitForSeconds(1);
   Seat(0);teacherMotion.Present();yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-tap.png"));yield return new WaitForSeconds(.9f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-present.png"));yield return new WaitForSeconds(1.6f);
   if(teacherMotion.IsPresenting)throw new Exception("Teacher gesture did not settle");
   ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-rest.png"));yield return new WaitForSeconds(.5f);
   Debug.Log("TEACHER_GESTURE_TEST_PASSED");
   playing=true;SetSection();Speak();
   yield return null;
   if(speech.isPlaying)throw new Exception("Narration started before presentation");
   playing=false;yield return new WaitForSeconds(.25f);
   if(speech.isPlaying)throw new Exception("Pending narration ignored pause");
   playing=true;
   for(int i=0;i<30&&speechLoading;i++)yield return new WaitForSeconds(.5f);
   if(speech.clip==null||speech.clip.samples<1)throw new Exception("Narration integration failed: "+status);
   yield return new WaitForSeconds(.5f);
   if(speech.time<=0)throw new Exception("Narration did not play");
   if(teacherMotion.IsPresenting||!teacherWalk.ReadyToTeach)throw new Exception("Narration overlapped unsettled teacher");
   if(board.text!=Wrap(Current.title+"\n\n"+Current.board,48))throw new Exception("Slide was overwritten during narration");
   Debug.Log("BOARD_STABILITY_TEST_PASSED");Debug.Log("NARRATION_PLAYER_TEST_PASSED");Debug.Log("LESSON_TIMING_TEST_PASSED");playing=false;speech.Stop();
   Seat(2);mode=1;boardFocus=true;yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"focus.png"));yield return new WaitForSeconds(1);
   boardFocus=false;book=true;bookTab=0;clock=12;Mark();Next(1);clock=20;Mark();
   if(!notes.Contains("00:02"))throw new Exception("Highlight lookback failed");
   var saved=JsonUtility.FromJson<LearningNotes>(File.ReadAllText(NotePath));if(saved.text!=notes)throw new Exception("Notes persistence failed");
   yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"book.png"));yield return new WaitForSeconds(1);
   mode=0;for(int tab=1;tab<4;tab++){bookTab=tab;yield return new WaitForSeconds(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"codex-tab-"+tab+".png"));yield return new WaitForSeconds(.5f);}
   book=false;Seat(0);yield return new WaitForSeconds(1);ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-front.png"));yield return new WaitForSeconds(1);
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--walk-test")>=0){
    book=false;Seat(2);mode=0;playing=true;walkingTest=true;teacherWalk.BeginTest();float deadline=Time.time+140;bool captured=false;
    while(teacherWalk.Trips<1&&Time.time<deadline){if(!captured&&professor.position.y>boardCenter.y-1.5f){ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-stairs.png"));captured=true;}yield return null;}
    playing=false;if(teacherWalk.Trips<1||teacherWalk.Travelled<5||!speech.transform.IsChildOf(professor))throw new Exception("Walking or attached audio test failed");
    Debug.Log("TEACHER_STAIRS_TEST_PASSED distance="+teacherWalk.Travelled+" audioPosition="+speech.transform.position);
    playing=true;teacherWalk.BeginTest();deadline=Time.time+40;
    while(teacherWalk.Travelled<5&&Time.time<deadline)yield return null;
    if(teacherWalk.Travelled<5)throw new Exception("Focus setup did not reach aisle");
    playing=false;yield return null;var stopped=professor.position;yield return new WaitForSeconds(1);
    if(Vector3.Distance(stopped,professor.position)>.001f)throw new Exception("Paused teacher kept walking");
    playing=true;mode=1;deadline=Time.time+80;
    while(!teacherWalk.AtHome&&Time.time<deadline)yield return null;
    if(!teacherWalk.AtHome)throw new Exception("Focus mode did not return teacher home");
    yield return new WaitForSeconds(3);stopped=professor.position;yield return new WaitForSeconds(3);
    if(Vector3.Distance(stopped,professor.position)>.001f)throw new Exception("Focus teacher started roaming");
    ScreenCapture.CaptureScreenshot(Path.Combine(dir,"teacher-focus-return.png"));yield return new WaitForSeconds(1);
    playing=false;Debug.Log("TEACHER_FOCUS_AND_PAUSE_TEST_PASSED");
   }
   if(Array.IndexOf(Environment.GetCommandLineArgs(),"--read-aloud-test")>=0)yield return ReadAloudSmoke();
   Debug.Log("AUDITORIUM_RUNTIME_SMOKE_PASSED");Application.Quit();
  }
 }
}





