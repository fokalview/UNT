const assert=require('node:assert/strict');
const fs=require('node:fs');const os=require('node:os');const path=require('node:path');
const root=fs.mkdtempSync(path.join(os.tmpdir(),'clarenza-roles-test-'));process.env.CLARENZA_DATA_DIR=root;
const accounts=require('./accounts');const {createServer}=require('./server');
(async()=>{const server=createServer();await new Promise(r=>server.listen(0,'127.0.0.1',r));
async function post(route,body={},token=''){const r=await fetch('http://127.0.0.1:'+server.address().port+route,{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+token},body:JSON.stringify(body)});return {status:r.status,body:await r.json()};}
try{
accounts.addUser('owner','long-admin-password','admin');
let r=await post('/auth/login',{username:'owner',password:'wrong-password'});assert.equal(r.status,401);
const admin=(await post('/auth/login',{username:'owner',password:'long-admin-password'})).body.token;
for(const [username,role] of [['teacher','teacher'],['student','student'],['student2','student']])assert.equal((await post('/accounts/create',{username,role,password:'long-user-password'},admin)).status,200);
const teacher=(await post('/auth/login',{username:'teacher',password:'long-user-password'})).body.token;
const student=(await post('/auth/login',{username:'student',password:'long-user-password',role:'admin'})).body.token;
const student2=(await post('/auth/login',{username:'student2',password:'long-user-password'})).body.token;
const document={title:'Circuits',sources:['A battery supplies energy.'],sections:[{title:'Battery',narration:'A battery supplies energy.',board:'Battery: energy',sourceIds:[1]}],quiz:[]};
assert.equal((await post('/catalog/save',{scope:'published',reviewed:true,document})).status,401);
assert.equal((await post('/catalog/save',{scope:'published',reviewed:true,document,role:'teacher'},student)).status,403);
assert.equal((await post('/accounts/create',{username:'intruder',password:'long-user-password',role:'admin'},student)).status,403);
assert.equal((await post('/accounts/create',{username:'intruder',password:'long-user-password',role:'admin'},teacher)).status,403);
assert.equal((await post('/catalog/save',{scope:'published',reviewed:true,document},teacher)).status,200);
const privateId=(await post('/catalog/save',{scope:'study',document},student)).body.id;
assert.equal((await post('/catalog/get',{id:privateId},student)).status,200);
assert.equal((await post('/catalog/get',{id:privateId},student2)).status,404);
assert.equal((await post('/catalog/get',{id:privateId},teacher)).status,404);
assert.equal((await post('/catalog/list',{},student)).body.items.length,2);
assert.equal((await post('/catalog/list',{},student2)).body.items.length,1);
await post('/auth/logout',{},student);assert.equal((await post('/catalog/list',{},student)).status,401);
console.log('PASS: login, server-assigned roles, admin provisioning, teacher publication, denied student publication, private-study isolation, catalog visibility, logout revocation.');
}finally{await new Promise(r=>server.close(r));fs.rmSync(root,{recursive:true,force:true});}
})().catch(e=>{console.error(e);process.exitCode=1;});
