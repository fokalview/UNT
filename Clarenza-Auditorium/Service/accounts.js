const fs=require('node:fs');
const path=require('node:path');
const crypto=require('node:crypto');
const root=process.env.CLARENZA_DATA_DIR||path.join(process.env.LOCALAPPDATA||require('node:os').homedir(),'Clarenza','Accounts');
function fail(message,status=400){const e=new Error(message);e.status=status;throw e;}
function read(){try{return JSON.parse(fs.readFileSync(path.join(root,'accounts.json'),'utf8'));}catch(e){if(e.code==='ENOENT')return {users:[],lessons:[]};throw e;}}
function write(db){fs.mkdirSync(root,{recursive:true});const temp=path.join(root,'accounts.tmp');fs.writeFileSync(temp,JSON.stringify(db));fs.renameSync(temp,path.join(root,'accounts.json'));}
function addUser(username,password,role){
 if(typeof username!=='string'||! /^[a-zA-Z0-9_.-]{3,40}$/.test(username))fail('Username: 3–40 letters, digits, dots, dashes or underscores.');
 if(typeof password!=='string'||password.length<12||password.length>128)fail('Use a password of 12–128 characters.');
 if(!['admin','teacher','student'].includes(role))fail('Invalid role.');
 const db=read();if(db.users.some(u=>u.username.toLowerCase()===username.toLowerCase()))fail('Username already exists.');
 const salt=crypto.randomBytes(16).toString('hex');const user={id:crypto.randomUUID(),username,role,salt,hash:crypto.scryptSync(password,salt,64).toString('hex')};db.users.push(user);write(db);return {id:user.id,username,role};
}
const sessions=new Map(),attempts=new Map();
function authenticate(token){const s=sessions.get(token);if(!s||s.expires<Date.now()){sessions.delete(token);fail('Please sign in again.',401);}const u=read().users.find(u=>u.id===s.id);if(!u)fail('Account unavailable.',401);return u;}
function lessonValid(l){if(!l||typeof l.title!=='string'||!l.title.trim()||l.title.length>200||!Array.isArray(l.sources)||l.sources.length<1||l.sources.length>100||l.sources.some(s=>typeof s!=='string')||!Array.isArray(l.sections)||!l.sections.length||l.sections.length>100)fail('Invalid lesson.');for(const s of l.sections){if(!s||typeof s.title!=='string'||typeof s.narration!=='string'||typeof s.board!=='string'||!Array.isArray(s.sourceIds)||s.sourceIds.some(i=>!Number.isInteger(i)||i<1||i>l.sources.length))fail('Invalid lesson section.');}for(const q of l.quiz||[]){if(!q||typeof q.question!=='string'||typeof q.explanation!=='string'||!Array.isArray(q.options)||q.options.length!==3||q.options.some(o=>typeof o!=='string')||!Number.isInteger(q.correct)||q.correct<0||q.correct>2||!Array.isArray(q.sourceIds)||!q.sourceIds.length||q.sourceIds.some(i=>!Number.isInteger(i)||i<1||i>l.sources.length))fail('Invalid quiz.');}if(JSON.stringify(l).length>200000)fail('Lesson too large.');}
function route(url,body,token){
 if(url==='/auth/login'){
 const key=String(body.username||'').toLowerCase();const a=attempts.get(key)||{count:0,until:Date.now()+60000};if(a.until<Date.now()){a.count=0;a.until=Date.now()+60000;}if(++a.count>8)fail('Too many attempts. Wait one minute.',429);attempts.set(key,a);
 const user=read().users.find(u=>u.username.toLowerCase()===key);const supplied=crypto.scryptSync(typeof body.password==='string'?body.password.slice(0,128):'',user?user.salt:'invalid-user',64);
 if(!user||!crypto.timingSafeEqual(supplied,Buffer.from(user.hash,'hex')))fail('Incorrect username or password.',401);
 attempts.delete(key);const token=crypto.randomBytes(32).toString('hex');sessions.set(token,{id:user.id,expires:Date.now()+8*3600000});return {token,id:user.id,username:user.username,role:user.role};
 }
 const user=authenticate(token);
 if(url==='/auth/logout'){sessions.delete(token);return {ok:true};}
 if(url==='/accounts/create'){if(user.role!=='admin')fail('Only administrators can create accounts.',403);return addUser(body.username,body.password,body.role);}
 if(url==='/catalog/list'){
  return {items:read().lessons.filter(l=>l.scope==='published'||l.owner===user.id).map(l=>({id:l.id,title:l.document.title,scope:l.scope,ownerId:l.owner,version:l.document.version||1,approval:l.document.approval||'legacy',classType:l.document.classType||'source'}))};
 }
 if(url==='/catalog/get'){const l=read().lessons.find(l=>l.id===body.id);if(!l||(l.scope!=='published'&&l.owner!==user.id))fail('Lesson unavailable.',404);return {...l.document,id:l.id,ownerId:l.owner};}
 if(url==='/catalog/save'){
  if(!['study','draft','published'].includes(body.scope))fail('Choose study, draft or published.');
  if(body.scope!=='study'&&!['teacher','admin'].includes(user.role))fail('Students can upload only to private study mode.',403);
  if(body.scope==='published'&&body.reviewed!==true)fail('Review the lesson and explicitly approve publication.');
  lessonValid(body.document);const db=read();
  const parent=body.parentId?db.lessons.find(l=>l.id===body.parentId):null;
  if(body.parentId&&(!parent||parent.owner!==user.id))fail('Only the owner can revise this lesson.',403);
  const familyId=parent?(parent.document.familyId||parent.id):crypto.randomUUID();
  const versions=db.lessons.filter(l=>(l.document.familyId||l.id)===familyId);
  const version=1+Math.max(0,...versions.map(l=>l.document.version||1));
  if(parent&&version!==(parent.document.version||1)+1)fail('A newer version exists. Open the latest version before editing.',409);
  const id=crypto.randomUUID(),approved=body.scope==='published';
  const document={...body.document,id,familyId,version,ownerId:user.id,parentId:parent?parent.id:'',approval:approved?'teacher-approved':body.scope==='draft'?'draft':'unreviewed',approvedBy:approved?user.username:'',approvedAt:approved?new Date().toISOString():'',classType:approved?'teacher':body.document.classType==='ai'?'ai':'source'};
  db.lessons.push({id,owner:user.id,scope:body.scope,document});write(db);return {id,ok:true,document};
 }
 fail('Unknown account route.',404);
}
module.exports={route,authenticate,addUser};
if(require.main===module){let input='';process.stdin.on('data',c=>input+=c);process.stdin.on('end',()=>{try{const b=JSON.parse(input);console.log(JSON.stringify(addUser(b.username,b.password,b.role)));}catch(e){console.error(e.message);process.exitCode=1;}});}
