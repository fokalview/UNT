using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

namespace Clarenza {
 [Serializable] public class LessonSection { public string title, narration, board, gesture; public int[] sourceIds; }
 [Serializable] public class LessonQuestion { public string question, explanation; public string[] options; public int correct; public int[] sourceIds; }
 [Serializable] public class LessonDocument:LessonIdentity { public string title, subject, mode; public string[] sources; public LessonSection[] sections; public LessonQuestion[] quiz; }
 [Serializable] public class TutorRequest { public string[] sources; public string question, style; }
 [Serializable] public class TutorAnswer { public string answer; public int[] sourceIds; }
 [Serializable] public class ServiceHealth { public bool ready, aiReady, speechReady; }
 [Serializable] public class SpeechRequest { public string text; }
 [Serializable] public class SpeechResponse { public string wav; }
 [Serializable] public class ServiceError { public string error; }
 [Serializable] public class LearningNotes { public string text; }
 public static class LessonImport {
  public static string Read(string path) {
   var file = new FileInfo(path);
   if(!file.Exists) throw new Exception("File not found. Paste the full path to a .txt, .md, or .pptx file.");
   if(file.Length>25*1024*1024) throw new Exception("Please use a file smaller than 25 MB for this pilot.");
   string ext=file.Extension.ToLowerInvariant();
   if(ext==".txt"||ext==".md") return File.ReadAllText(path);
   if(ext!=".pptx") throw new Exception("Supported formats: .txt, .md and .pptx. Export older PowerPoint files to .pptx first.");
   using(var fileStream=File.OpenRead(path)) using(var archive=new ZipArchive(fileStream,ZipArchiveMode.Read)) {
    var slides=archive.Entries.Where(e=>Regex.IsMatch(e.FullName,@"^ppt/slides/slide\d+\.xml$")).OrderBy(e=>int.Parse(Regex.Match(e.FullName,@"slide(\d+)\.xml").Groups[1].Value)).ToArray();
    if(slides.Length>100 || slides.Sum(e=>e.Length)>5000000) throw new Exception("Presentation is too large for this pilot.");
    XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
    return string.Join("\n\n",slides.Select((e,i)=>{
     using(var stream=e.Open()) { var doc=XDocument.Load(stream); return "Slide "+(i+1)+"\n"+string.Join("\n",doc.Descendants(a+"p").Select(p=>string.Concat(p.Descendants(a+"t").Select(t=>t.Value)))); }
    }));
   }
  }
  public static string[] Sources(string text) {
   if(string.IsNullOrWhiteSpace(text)) throw new Exception("This file has no readable text. Image-only slides need text added first.");
   if(text.Length>50000) throw new Exception("Please import up to 50,000 characters at a time.");
   var chunks=Regex.Split(text.Trim(),@"\r?\n\s*\r?\n").Where(s=>!string.IsNullOrWhiteSpace(s)).SelectMany(s=>Enumerable.Range(0,(s.Length+1199)/1200).Select(i=>s.Substring(i*1200,Math.Min(1200,s.Length-i*1200)))).ToArray();
   if(chunks.Length>100) throw new Exception("Please split this document into fewer than 100 passages.");
   return chunks;
  }
  public static LessonDocument Local(string text,string title) {
   var src=Sources(text);
   return new LessonDocument { title=title,subject="Imported lesson",mode="Source playback — not AI generated",sources=src,
    sections=src.Select((s,i)=>new LessonSection {title="Passage "+(i+1), narration=s,board=s.Length>220?s.Substring(0,220)+"…":s,gesture=i%2==0?"explain":"point",sourceIds=new[]{i+1}}).ToArray(),quiz=new LessonQuestion[0] };
  }
  public static void Validate(LessonDocument l) {
   if(l==null||string.IsNullOrWhiteSpace(l.title)||l.sections==null||l.sections.Length==0||l.sources==null||l.sources.Length==0) throw new Exception("Lesson is incomplete.");
   foreach(var s in l.sections) {
    if(s==null||string.IsNullOrWhiteSpace(s.title)||string.IsNullOrWhiteSpace(s.narration)||string.IsNullOrWhiteSpace(s.board)||s.sourceIds==null||s.sourceIds.Length==0||s.sourceIds.Any(i=>i<1||i>l.sources.Length)) throw new Exception("Lesson section has missing content or invalid source references.");
   }
   foreach(var q in l.quiz??new LessonQuestion[0]) if(q==null||q.options==null||q.options.Length!=3||q.correct<0||q.correct>2||q.sourceIds==null||q.sourceIds.Any(i=>i<1||i>l.sources.Length)) throw new Exception("Quiz data is invalid.");
  }
  public static AudioClip Wav(byte[] bytes) {
   using(var reader=new BinaryReader(new MemoryStream(bytes))) {
    if(new string(reader.ReadChars(4))!="RIFF") throw new Exception("Invalid narration audio.");
    reader.ReadInt32(); if(new string(reader.ReadChars(4))!="WAVE") throw new Exception("Invalid wave audio.");
    int channels=1,rate=22050,bits=16,format=1; byte[] data=null;
    while(reader.BaseStream.Position+8<=bytes.Length) {
     string id=new string(reader.ReadChars(4)); int size=reader.ReadInt32(); long next=reader.BaseStream.Position+size+(size%2);
     if(size<0||next>bytes.Length) throw new Exception("Invalid audio chunk.");
     if(id=="fmt ") {format=reader.ReadInt16();channels=reader.ReadInt16();rate=reader.ReadInt32();reader.ReadInt32();reader.ReadInt16();bits=reader.ReadInt16();}
     if(id=="data") data=reader.ReadBytes(size);
     reader.BaseStream.Position=next;
    }
    if(data==null||format!=1||bits!=16||channels<1) throw new Exception("Narration must be PCM 16-bit audio.");
    float[] samples=new float[data.Length/2];for(int i=0;i<samples.Length;i++) samples[i]=BitConverter.ToInt16(data,i*2)/32768f;
    var clip=AudioClip.Create("Professor narration",samples.Length/channels,channels,rate,false);clip.SetData(samples,0);return clip;
   }
  }
 }
}
