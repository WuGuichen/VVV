"use strict";
// One shared WebGL context renders all visible cards. Assets are the same as Unity.
(() => {
const root=document.getElementById('cards-view');
const asset='/Assets/Resources/MxFramework/UI/CardTheme/Icons/';
const utilityAsset='/Assets/Resources/MxFramework/UI/CardTheme/Utility/';
let icons={},configReady=false,artworkRevision="";
let qualities={},renderer;
const visible=new Set(),tracked=new Set();
const observer=new IntersectionObserver(entries=>entries.forEach(e=>{if(e.isIntersecting)visible.add(e.target);else visible.delete(e.target)}));
function sync(){if(!configReady)return;root.querySelectorAll('.tactical-face:not([data-theme-ready])').forEach(face=>{
face.dataset.themeReady='true';const id=face.dataset.styleId;const art=face.querySelector('.face-art');
const utility=face.dataset.utilityIllustration;
const illustration=utility||icons[id]||'IdlePenOil';art.src=(utility?utilityAsset:asset)+encodeURIComponent(illustration)+'.png?v='+encodeURIComponent(artworkRevision);art.classList.add('painted');
observer.observe(face);tracked.add(face);
});for(const face of tracked)if(!face.isConnected){visible.delete(face);tracked.delete(face);observer.unobserve(face);}}
new MutationObserver(sync).observe(root,{childList:true,subtree:true});sync();
const vert=`attribute vec2 a_position;varying vec2 uv;void main(){uv=(a_position+1.)*.5;gl_Position=vec4(a_position,0.,1.);}`;
const frag=`precision highp float;varying vec2 uv;uniform sampler2D art;uniform sampler2D regions;uniform float quality;uniform float time;uniform float utility;
vec3 hsv(vec3 c){vec4 K=vec4(0.,-1./3.,2./3.,-1.);vec4 p=mix(vec4(c.bg,K.wz),vec4(c.gb,K.xy),step(c.b,c.g));vec4 q=mix(vec4(p.xyw,c.r),vec4(c.r,p.yzx),step(p.x,c.r));float d=q.x-min(q.w,q.y),e=1.e-7;return vec3(abs(q.z+(q.w-q.y)/(6.*d+e)),d/(q.x+e),q.x);}
vec3 rgb(vec3 c){vec3 p=abs(fract(c.xxx+vec3(0.,2./3.,1./3.))*6.-3.);return c.z*mix(vec3(1.),clamp(p-1.,0.,1.),c.y);}
void main(){
vec4 src=texture2D(art,uv),m=texture2D(regions,uv);vec3 h=hsv(src.rgb);float zone=clamp(m.r+m.g+m.b,0.,1.);vec3 base=src.rgb;
if(quality<1.5){base=rgb(vec3(.58,min(h.y,.14),h.z));zone=max(zone,smoothstep(.28,.48,h.y));}
vec3 v=h;
if(quality<.5){v.x=.58;v.y*=.2;}else if(quality<1.5){v.x=.117;v.y=clamp(v.y*1.06,0.,1.);}else{v.x+=time*.125+.045*sin(uv.x*5.+uv.y*4.-time*.8);}
float strength=1.;float w=sin((uv.x-uv.y*.68)*150.+time*.5+sin(uv.y*14.+time*.3)*.22);v.y*=1.-(.5+.5*w)*.30*m.g*strength;v.z*=1.-(.5-.5*w)*.035*m.g*strength;
float opticalZone=quality>=1.5?clamp(m.r+m.g,0.,1.):zone;vec3 color=mix(base,rgb(v),opticalZone);float grain=(fract(sin(dot(floor(uv*vec2(756.,1056.)),vec2(127.1,311.7)))*43758.5453)-.5)*.013;color=mix(color,min(color,vec3(.932,.96,.982))+grain,(1.-zone)*.8);if(utility>.5){float nearInset=min(texture2D(regions,uv+vec2(.006,0.)).g,texture2D(regions,uv-vec2(.006,0.)).g);nearInset=min(nearInset,min(texture2D(regions,uv+vec2(0.,.004)).g,texture2D(regions,uv-vec2(0.,.004)).g));float innerBevel=m.g*(1.-nearInset);float engraving=smoothstep(.24,.44,fract((uv.x+uv.y*.68)*11.));color=mix(color,vec3(.22,.32,.40),innerBevel*engraving*.45);}gl_FragColor=vec4(color,m.a);}`;

function load(src){return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(Error(src));im.src=src;});}
function compile(gl,type,source){const s=gl.createShader(type);gl.shaderSource(s,source);gl.compileShader(s);if(!gl.getShaderParameter(s,gl.COMPILE_STATUS))throw Error(gl.getShaderInfoLog(s));return s;}
async function init(){
const [art,mask,config]=await Promise.all([load('/Assets/MxFrameworkGenerated/UI/CardShellStudy/ShellArtwork.png'),load('/Assets/MxFrameworkGenerated/UI/CardShellStudy/AuthoredRegionMask.png'),fetch('/Assets/Resources/MxFramework/UI/CardTheme/PreparationQualityStyles.json').then(r=>{if(!r.ok)throw Error(r.status);return r.json()})]);
artworkRevision=config.artworkRevision||"";
qualities=Object.fromEntries(config.cards.map(x=>[x.cardId,x.quality]));
icons=Object.fromEntries(config.cards.filter(x=>x.illustration).map(x=>[x.cardId,x.illustration]));configReady=true;sync();
const c=document.createElement('canvas');c.width=756;c.height=1056;const gl=c.getContext('webgl',{preserveDrawingBuffer:true,premultipliedAlpha:false});if(!gl)throw Error('WebGL unavailable');
const p=gl.createProgram();gl.attachShader(p,compile(gl,gl.VERTEX_SHADER,vert));gl.attachShader(p,compile(gl,gl.FRAGMENT_SHADER,frag));gl.linkProgram(p);if(!gl.getProgramParameter(p,gl.LINK_STATUS))throw Error(gl.getProgramInfoLog(p));gl.useProgram(p);
const b=gl.createBuffer();gl.bindBuffer(gl.ARRAY_BUFFER,b);gl.bufferData(gl.ARRAY_BUFFER,new Float32Array([-1,-1,1,-1,-1,1,-1,1,1,-1,1,1]),gl.STATIC_DRAW);const a=gl.getAttribLocation(p,'a_position');gl.enableVertexAttribArray(a);gl.vertexAttribPointer(a,2,gl.FLOAT,false,0,0);
const textures=[art,mask].map(im=>{const texture=gl.createTexture();gl.bindTexture(gl.TEXTURE_2D,texture);gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL,true);for(const prop of [gl.TEXTURE_MIN_FILTER,gl.TEXTURE_MAG_FILTER])gl.texParameteri(gl.TEXTURE_2D,prop,gl.LINEAR);for(const prop of [gl.TEXTURE_WRAP_S,gl.TEXTURE_WRAP_T])gl.texParameteri(gl.TEXTURE_2D,prop,gl.CLAMP_TO_EDGE);gl.texImage2D(gl.TEXTURE_2D,0,gl.RGBA,gl.RGBA,gl.UNSIGNED_BYTE,im);return texture;});
gl.uniform1i(gl.getUniformLocation(p,'art'),0);gl.uniform1i(gl.getUniformLocation(p,'regions'),1);
renderer={c,gl,p,textures,q:gl.getUniformLocation(p,'quality'),t:gl.getUniformLocation(p,'time'),u:gl.getUniformLocation(p,'utility')};
}
let last=0;const reduced=matchMedia('(prefers-reduced-motion: reduce)');
function frame(now){requestAnimationFrame(frame);if(!renderer||document.hidden||root.hidden||now-last<80)return;last=now;
const groups=[[],[],[],[],[],[]];for(const face of visible){if(!face.isConnected)continue;if(!face.getClientRects().length)continue;groups[Math.max(0,Math.min(2,qualities[face.dataset.styleId]||0))+(face.dataset.utilityIllustration?3:0)].push(face);}
const {c,gl,p,q,t,u,textures}=renderer;gl.useProgram(p);gl.viewport(0,0,c.width,c.height);gl.uniform1f(t,reduced.matches?0:now/1000);
groups.forEach((faces,i)=>{if(!faces.length)return;const utility=i>=3;gl.uniform1f(q,i%3);gl.uniform1f(u,utility?1:0);for(let slot=0;slot<2;slot++){gl.activeTexture(gl.TEXTURE0+slot);gl.bindTexture(gl.TEXTURE_2D,textures[slot]);}gl.drawArrays(gl.TRIANGLES,0,6);for(const face of faces){const out=face.querySelector('.face-shell');if(!out)continue;const w=Math.round(face.clientWidth*Math.min(devicePixelRatio,2)),h=Math.round(w*528/378);if(out.width!==w||out.height!==h){out.width=w;out.height=h;}out.getContext('2d').clearRect(0,0,w,h);out.getContext('2d').drawImage(c,0,0,w,h);face.classList.add('theme-rendered');}});
}
init().then(()=>requestAnimationFrame(frame)).catch(error=>{root.dataset.themeError='true';console.error('Card theme assets failed',error);});
})();
