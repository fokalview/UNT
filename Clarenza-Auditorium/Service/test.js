const assert=require('node:assert/strict');
const {dispatch,validateSources,validateLesson,createServer}=require('./server');
(async()=>{
 assert.throws(()=>validateSources([0],2));assert.throws(()=>validateSources([3],2));validateSources([1,2],2);
 assert.throws(()=>validateLesson({sections:[],quiz:[]},1));
 const valid={title:'Test',sections:[{title:'A',narration:'B',board:'C',sourceIds:[1]}],quiz:[{question:'Q',options:['a','b','c'],correct:1,explanation:'E',sourceIds:[1]}]};
 validateLesson(valid,1);assert.throws(()=>validateLesson({...valid,quiz:[{...valid.quiz[0],correct:3}]},1));
 await assert.rejects(()=>dispatch('/lesson',{sources:[]}));
 const oldKey=process.env.OPENAI_API_KEY,oldModel=process.env.OPENAI_MODEL,oldFetch=global.fetch;
 try {
  process.env.OPENAI_API_KEY='test-only';process.env.OPENAI_MODEL='test-model';
  global.fetch=async(url,options)=>{const request=JSON.parse(options.body);assert.equal(request.store,false);assert.equal(request.text.format.type,'json_schema');return {ok:true,json:async()=>({status:'completed',output:[{content:[{type:'output_text',text:JSON.stringify(valid)}]}]})};};
  assert.equal((await dispatch('/lesson',{sources:['Source paragraph.']})).title,'Test');
  global.fetch=async()=>({ok:true,json:async()=>({status:'incomplete',output:[]})});
  await assert.rejects(()=>dispatch('/lesson',{sources:['Source paragraph.']}),/did not complete/);
 } finally {
  global.fetch=oldFetch;
  if(oldKey===undefined)delete process.env.OPENAI_API_KEY;else process.env.OPENAI_API_KEY=oldKey;
  if(oldModel===undefined)delete process.env.OPENAI_MODEL;else process.env.OPENAI_MODEL=oldModel;
 }
 const service=createServer();await new Promise(r=>service.listen(0,'127.0.0.1',r));
 try {
  const url='http://127.0.0.1:'+service.address().port+'/health';
  assert.equal((await fetch(url)).status,403);
  assert.equal((await fetch(url,{method:'POST',headers:{'Content-Type':'application/json',Origin:'https://example.com'},body:'{}'})).status,403);
  assert.equal((await (await fetch(url,{method:'POST',headers:{'Content-Type':'application/json'},body:'{}'})).json()).ready,true);
 }finally{service.close();}
 console.log('SERVICE_TESTS_PASSED');
})().catch(e=>{console.error(e);process.exitCode=1});
