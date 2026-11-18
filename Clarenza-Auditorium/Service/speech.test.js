const assert=require('node:assert/strict');
const {dispatch}=require('./server');
(async()=>{
 for(const rate of [-3,3,1.5,'fast',null])await assert.rejects(()=>dispatch('/speech',{text:'Test',rate}),/rate/);
 await assert.rejects(()=>dispatch('/speech',{text:'',rate:0}),/Speech text/);
 await assert.rejects(()=>dispatch('/speech',{text:'a'.repeat(5001),rate:0}),/Speech text/);
 for(const rate of [-2,0,2]){
  const result=await dispatch('/speech',{text:'A closed switch completes the circuit.',rate});
  const wav=Buffer.from(result.wav,'base64');assert.equal(wav.subarray(0,4).toString(),'RIFF');assert.equal(wav.subarray(8,12).toString(),'WAVE');assert.ok(wav.length>100);
 }
 console.log('SPEECH_TESTS_PASSED: real local synthesis at three rates, WAV headers, empty/oversize/rate rejection. No paid provider.');
})().catch(e=>{console.error(e);process.exitCode=1;});
