// form2d_ui.js — 2D-Formfindung UI (Canvas-Modal): STL-Schnitt-Silhouette als Gebiet + Lager/Kraefte zeichnen + SIMP/SKO.
(function(){
'use strict';
const $=id=>document.getElementById(id);
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
let cv, cx, W=800, H=500, nelx=96, nely=60;
const items=[];              // {kind:'sup'|'for', shape:'rect'|'circle', x,y, w,h | r, fx,fy}
let sel=-1, pend=null, drag=null, density=null, running=false;
// STL-Schnitt
let stlTris=null, secSegs=null, dom=null;     // dom=Uint8Array(nelx*nely) oder null (=Rechteck)
const rot={x:0,y:0,z:0}; let slc=0.5;

function px2elem(){ return {ex:W/nelx, ey:H/nely}; }
function hit(it,px,py){ if(it.shape==='circle'){ const dx=px-it.x,dy=py-it.y; return dx*dx+dy*dy<=it.r*it.r; }
  return px>=it.x && px<=it.x+it.w && py>=it.y && py<=it.y+it.h; }
function itemCenter(it){ return it.shape==='circle'?[it.x,it.y]:[it.x+it.w/2,it.y+it.h/2]; }
function itemNodes(it){ const {ex,ey}=px2elem(), ns=[]; for(let i=0;i<=nelx;i++)for(let j=0;j<=nely;j++){ if(hit(it,i*ex,j*ey)) ns.push(i*(nely+1)+j); } return ns; }
function activeNodes(){ if(!dom)return null; const na=new Uint8Array((nelx+1)*(nely+1));
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ if(!dom[ex*nely+ey])continue; na[ex*(nely+1)+ey]=1; na[(ex+1)*(nely+1)+ey]=1; na[(ex+1)*(nely+1)+ey+1]=1; na[ex*(nely+1)+ey+1]=1; } return na; }

// ---- STL-Schnitt ----
function loadStl(file){ if(!(window.THREE&&THREE.STLLoader)){ $('f2dInfo').textContent='THREE/STLLoader nicht verfügbar.'; return; }
  const rd=new FileReader(); rd.onload=ev=>{ try{ const g=new THREE.STLLoader().parse(ev.target.result), pos=g.attributes.position;
    stlTris=[]; for(let i=0;i+2<pos.count;i+=3) stlTris.push([[pos.getX(i),pos.getY(i),pos.getZ(i)],[pos.getX(i+1),pos.getY(i+1),pos.getZ(i+1)],[pos.getX(i+2),pos.getY(i+2),pos.getZ(i+2)]]);
    computeSection(); density=null; draw(); $('f2dInfo').textContent='STL: '+stlTris.length+' Dreiecke — Schnitt-Silhouette als Gebiet.';
  }catch(err){ $('f2dInfo').textContent='STL-Fehler: '+err.message; } }; rd.readAsArrayBuffer(file); }
function clearStl(){ stlTris=null; secSegs=null; dom=null; density=null; draw(); $('f2dInfo').textContent='Rechteck-Gebiet.'; }
function computeSection(){ if(!stlTris){ dom=null; secSegs=null; return; }
  const M=new THREE.Matrix4().makeRotationFromEuler(new THREE.Euler(rot.x*Math.PI/180,rot.y*Math.PI/180,rot.z*Math.PI/180,'XYZ'));
  const v=new THREE.Vector3(), tv=[null,null,null];
  let zmn=1e9,zmx=-1e9; for(const t of stlTris)for(const a of t){ v.set(a[0],a[1],a[2]).applyMatrix4(M); if(v.z<zmn)zmn=v.z; if(v.z>zmx)zmx=v.z; }
  const zcut=zmn+clamp(slc,0,1)*(zmx-zmn||1);
  const segs=[];
  for(const t of stlTris){ for(let a=0;a<3;a++){ v.set(t[a][0],t[a][1],t[a][2]).applyMatrix4(M); tv[a]=[v.x,v.y,v.z]; }
    const s=[]; for(let a=0;a<3;a++){ const A=tv[a],B=tv[(a+1)%3], za=A[2]-zcut, zb=B[2]-zcut; if((za>0)!==(zb>0)){ const tt=za/(za-zb); s.push([A[0]+(B[0]-A[0])*tt, A[1]+(B[1]-A[1])*tt]); } }
    if(s.length===2) segs.push([s[0],s[1]]); }
  if(!segs.length){ secSegs=[]; dom=new Uint8Array(nelx*nely); return; }
  let mnx=1e9,mny=1e9,mxx=-1e9,mxy=-1e9; for(const s of segs)for(const q of s){ if(q[0]<mnx)mnx=q[0];if(q[0]>mxx)mxx=q[0];if(q[1]<mny)mny=q[1];if(q[1]>mxy)mxy=q[1]; }
  const sc=Math.min(W*0.88/Math.max(1e-6,mxx-mnx), H*0.88/Math.max(1e-6,mxy-mny)), ox=(W-(mxx-mnx)*sc)/2, oy=(H-(mxy-mny)*sc)/2;
  const map=q=>[ (q[0]-mnx)*sc+ox, H-((q[1]-mny)*sc+oy) ];   // Welt-xy -> px (y gespiegelt)
  secSegs=segs.map(s=>[map(s[0]),map(s[1])]);
  dom=new Uint8Array(nelx*nely);
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ const X=(ex+0.5)*W/nelx, Y=(ey+0.5)*H/nely+1.3e-3; let c=0;
    for(const s of secSegs){ const a=s[0],b=s[1]; if((a[1]>Y)!==(b[1]>Y)){ const xi=a[0]+(Y-a[1])/(b[1]-a[1])*(b[0]-a[0]); if(xi>X) c++; } }
    dom[ex*nely+ey]=(c&1)?1:0; }
}

function buildInputs(){ const nnode=(nelx+1)*(nely+1), fixed=new Uint8Array(2*nnode), loads=[], na=activeNodes();
  for(const it of items) if(it.kind==='sup') for(const n of itemNodes(it)){ fixed[2*n]=1; fixed[2*n+1]=1; }
  for(const it of items) if(it.kind==='for'){ let ns=itemNodes(it); if(na) ns=ns.filter(n=>na[n]); if(!ns.length)continue; const fx=it.fx/ns.length, fy=it.fy/ns.length;
    for(const n of ns){ if(fx) loads.push([2*n,fx]); if(fy) loads.push([2*n+1,fy]); } }
  const o={nelx,nely,fixed,loads}; if(dom) o.dom=dom; return o;
}
function run(mode){ if(running||!window.Form2D){ if(!window.Form2D) $('f2dInfo').textContent='Engine form2d.js nicht geladen.'; return; }
  if(!items.some(i=>i.kind==='sup')||!items.some(i=>i.kind==='for')){ $('f2dInfo').textContent='Mindestens ein Lager UND eine Kraft zeichnen.'; return; }
  running=true; $('f2dInfo').textContent = mode==='sko'?'SKO-Vorschau…':'SIMP rechnet…';
  setTimeout(()=>{ try{ const inp=buildInputs(), vf=clamp((+$('f2dVf').value||40)/100,0.05,0.9), rmin=Math.max(1.2,+$('f2dRmin').value||1.6), t0=performance.now();
    let r; if(mode==='sko') r=window.Form2D.sko2D(Object.assign(inp,{volfrac:vf,iters:10,rmin}));
    else r=window.Form2D.simp2D(Object.assign(inp,{volfrac:vf,iters:clamp(+$('f2dIt').value||45,5,200),rmin}));
    density=r.x; draw(); $('f2dInfo').textContent=(mode==='sko'?'SKO':'SIMP')+' fertig — '+((performance.now()-t0)/1000).toFixed(2)+' s, '+nelx+'×'+nely+'.';
  }catch(err){ $('f2dInfo').textContent='Fehler: '+err.message; } running=false; },30);
}

function draw(){ if(!cx)return; cx.fillStyle='#0e1116'; cx.fillRect(0,0,W,H);
  if(density||dom){ const img=cx.createImageData(nelx,nely);
    for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ let v; if(density) v=clamp(density[ex*nely+ey],0,1); else v=dom[ex*nely+ey]?0.28:0;
      const p=4*(ey*nelx+ex), g=Math.round(30+210*v); img.data[p]=g; img.data[p+1]=g; img.data[p+2]=g; img.data[p+3]=255; }
    const tc=draw._tc||(draw._tc=document.createElement('canvas')); tc.width=nelx; tc.height=nely; tc.getContext('2d').putImageData(img,0,0);
    cx.imageSmoothingEnabled=false; cx.drawImage(tc,0,0,W,H); cx.imageSmoothingEnabled=true;
  }
  if(secSegs&&secSegs.length){ cx.strokeStyle='#39d3ff'; cx.lineWidth=1.5; cx.beginPath(); for(const s of secSegs){ cx.moveTo(s[0][0],s[0][1]); cx.lineTo(s[1][0],s[1][1]); } cx.stroke(); }
  for(let k=0;k<items.length;k++){ const it=items[k], on=k===sel; cx.lineWidth=on?3:2;
    cx.strokeStyle=it.kind==='sup'?'#4fd86a':'#e08a4f'; cx.fillStyle=(it.kind==='sup'?'rgba(79,216,106,':'rgba(224,138,79,')+(on?'0.30)':'0.15)');
    cx.beginPath(); if(it.shape==='circle') cx.arc(it.x,it.y,it.r,0,7); else cx.rect(it.x,it.y,it.w,it.h); cx.fill(); cx.stroke();
    if(it.kind==='for'){ const c=itemCenter(it), L=Math.hypot(it.fx,it.fy)||1, s=clamp(L*0.06,16,70), ux=it.fx/L, uy=it.fy/L; arrow(c[0],c[1],c[0]+ux*s,c[1]+uy*s,'#ffd24a'); } }
  if(drag&&pend) previewDraw();
}
function arrow(x0,y0,x1,y1,col){ cx.strokeStyle=col; cx.fillStyle=col; cx.lineWidth=2.5; cx.beginPath(); cx.moveTo(x0,y0); cx.lineTo(x1,y1); cx.stroke();
  const a=Math.atan2(y1-y0,x1-x0),h=9; cx.beginPath(); cx.moveTo(x1,y1); cx.lineTo(x1-h*Math.cos(a-0.4),y1-h*Math.sin(a-0.4)); cx.lineTo(x1-h*Math.cos(a+0.4),y1-h*Math.sin(a+0.4)); cx.closePath(); cx.fill(); }
function previewDraw(){ cx.save(); cx.strokeStyle=pend.kind==='sup'?'#4fd86a':'#e08a4f'; cx.setLineDash([6,4]); cx.lineWidth=2;
  if(pend.shape==='circle'){ cx.beginPath(); cx.arc(drag.sx,drag.sy,Math.hypot(drag.px-drag.sx,drag.py-drag.sy),0,7); cx.stroke(); }
  else cx.strokeRect(Math.min(drag.sx,drag.px),Math.min(drag.sy,drag.py),Math.abs(drag.px-drag.sx),Math.abs(drag.py-drag.sy)); cx.restore(); }

function pos(e){ const r=cv.getBoundingClientRect(); return [ (e.clientX-r.left)*W/r.width, (e.clientY-r.top)*H/r.height ]; }
function down(e){ if(!cv||$('f2dModal').style.display==='none')return; const [px,py]=pos(e);
  if(pend){ drag={mode:'new',sx:px,sy:py,px,py}; return; }
  let k=-1; for(let i=items.length-1;i>=0;i--){ if(hit(items[i],px,py)){ k=i; break; } } sel=k; renderEditor();
  if(k>=0){ const it=items[k]; drag={mode:'move',idx:k, ox:px-it.x, oy:py-it.y}; if(cv)cv.style.cursor='move'; } draw(); }
function move(e){ if(!drag)return; const [px,py]=pos(e);
  if(drag.mode==='new'){ drag.px=px; drag.py=py; } else { const it=items[drag.idx]; it.x=px-drag.ox; it.y=py-drag.oy; } draw(); }
function up(e){ if(!drag)return; if(drag.mode==='new'){ const [px,py]=pos(e); const it=makeItem(drag.sx,drag.sy,px,py); if(it){ items.push(it); sel=items.length-1; } setTool(null); }
  drag=null; if(cv&&!pend)cv.style.cursor=''; renderEditor(); draw(); }
function makeItem(x0,y0,x1,y1){ if(!pend)return null; const kind=pend.kind, shape=pend.shape, fy0=kind==='for'?300:0;
  if(shape==='circle'){ const r=Math.hypot(x1-x0,y1-y0); if(r<5)return null; return {kind,shape,x:x0,y:y0,r,fx:0,fy:fy0}; }
  const x=Math.min(x0,x1),y=Math.min(y0,y1),w=Math.abs(x1-x0),h=Math.abs(y1-y0); if(w<5||h<5)return null; return {kind,shape,x,y,w,h,fx:0,fy:fy0}; }
function setTool(t){ pend=t; ['f2dSupR','f2dSupC','f2dForR','f2dForC'].forEach(id=>{ const b=$(id); if(b)b.classList.remove('on'); });
  if(t){ const id=t.kind==='sup'?(t.shape==='rect'?'f2dSupR':'f2dSupC'):(t.shape==='rect'?'f2dForR':'f2dForC'); const b=$(id); if(b)b.classList.add('on'); if(cv)cv.style.cursor='crosshair'; } else if(cv) cv.style.cursor=''; }
function renderEditor(){ const box=$('f2dEd'); if(!box)return; if(sel<0){ box.innerHTML=''; return; } const it=items[sel];
  let h='<div style="font-size:12px;color:'+(it.kind==='sup'?'#4fd86a':'#e08a4f')+';margin-bottom:4px">'+(it.kind==='sup'?'Lager':'Kraft')+' '+(sel+1)+' ('+(it.shape==='circle'?'Kreis':'Rechteck')+')</div>';
  if(it.kind==='for') h+='<div class="grid3"><div><label>Fx</label><input type="number" id="f2dFx" value="'+it.fx+'" step="50"></div><div><label>Fy (↓+)</label><input type="number" id="f2dFy" value="'+it.fy+'" step="50"></div><div></div></div>';
  h+='<button id="f2dDel" class="mini" style="margin-top:6px">✕ löschen</button>'; box.innerHTML=h;
  if($('f2dFx')){ const u=()=>{ it.fx=+$('f2dFx').value; it.fy=+$('f2dFy').value; draw(); }; $('f2dFx').addEventListener('input',u); $('f2dFy').addEventListener('input',u); }
  $('f2dDel').addEventListener('click',()=>{ items.splice(sel,1); sel=-1; renderEditor(); draw(); });
}
function resize(){ if(!cv)return; const par=cv.parentElement; W=Math.max(200,par.clientWidth); H=Math.max(150,par.clientHeight);
  nelx=clamp(Math.round(+($('f2dRes')&&$('f2dRes').value)||96),20,220); nely=Math.max(10,Math.round(nelx*H/W));
  cv.width=W; cv.height=H; if(stlTris) computeSection(); }
function show(v){ const m=$('f2dModal'); if(!m)return; m.style.display=v?'block':'none'; if(v){ resize(); density=null; draw(); } }
function init(){ cv=$('c2d'); if(!cv)return; cx=cv.getContext('2d');
  const on=(id,fn,ev)=>{ const b=$(id); if(b)b.addEventListener(ev||'click',fn); };
  on('btn2dOpen',()=>show(true)); on('f2dClose',()=>show(false));
  on('f2dSupR',()=>setTool({kind:'sup',shape:'rect'})); on('f2dSupC',()=>setTool({kind:'sup',shape:'circle'}));
  on('f2dForR',()=>setTool({kind:'for',shape:'rect'})); on('f2dForC',()=>setTool({kind:'for',shape:'circle'}));
  on('f2dRun',()=>run('simp')); on('f2dPrev',()=>run('sko'));
  on('f2dClr',()=>{ items.length=0; sel=-1; density=null; renderEditor(); draw(); });
  on('f2dReset',()=>{ density=null; draw(); });
  on('f2dStlBtn',()=>{ const f=$('f2dStlFile'); if(f)f.click(); });
  on('f2dStlFile',e=>{ const f=e.target.files&&e.target.files[0]; if(f) loadStl(f); e.target.value=''; },'change');
  on('f2dStlClr',clearStl);
  on('f2dExSvg',exportSVG); on('f2dExDxf',exportDXF); on('f2dExStl',exportSTL);
  const secUpd=()=>{ rot.x=+($('f2dRx').value||0); rot.y=+($('f2dRy').value||0); rot.z=+($('f2dRz').value||0); slc=clamp((+$('f2dSlice').value||50)/100,0,1); if(stlTris){ computeSection(); density=null; draw(); } };
  ['f2dRx','f2dRy','f2dRz','f2dSlice'].forEach(id=>{ const b=$(id); if(b)b.addEventListener('input',secUpd); });
  on('f2dRes',()=>{ resize(); density=null; draw(); },'change');
  cv.addEventListener('mousedown',down); window.addEventListener('mousemove',move); window.addEventListener('mouseup',up);
  window.addEventListener('resize',()=>{ if($('f2dModal')&&$('f2dModal').style.display!=='none'){ resize(); draw(); } });
}
// ---- Export (aktuelle Form: density>0.5 innerhalb dom) ----
function solidCells(){ if(!density) return null; const ne=nelx*nely, s=new Uint8Array(ne); for(let e=0;e<ne;e++) s[e]=(density[e]>0.5 && (!dom||dom[e]))?1:0; return s; }
function exportScale(){ const s=solidCells(); if(!s)return null; const cw=W/nelx, ch=H/nely;
  let bx0=1e9,by0=1e9,bx1=-1e9,by1=-1e9; for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ if(!s[ex*nely+ey])continue; if(ex*cw<bx0)bx0=ex*cw; if(ey*ch<by0)by0=ey*ch; if((ex+1)*cw>bx1)bx1=(ex+1)*cw; if((ey+1)*ch>by1)by1=(ey+1)*ch; }
  if(bx1<bx0) return null; const k=Math.max(0.001,+($('f2dExW').value||100))/Math.max(1e-6,bx1-bx0);
  return {s,cw,ch,bx0,by0,bx1,by1,k}; }
function download(name,text,mime){ const b=new Blob([text],{type:mime||'text/plain'}), a=document.createElement('a'); a.href=URL.createObjectURL(b); a.download=name; document.body.appendChild(a); a.click(); a.remove(); setTimeout(()=>URL.revokeObjectURL(a.href),1500); }
function boundaryEdges(SC){ const s=SC.s, cw=SC.cw, ch=SC.ch, edges=[], S=(ex,ey)=>(ex<0||ey<0||ex>=nelx||ey>=nely)?0:s[ex*nely+ey];
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ if(!s[ex*nely+ey])continue;
    if(!S(ex-1,ey)) edges.push([[ex*cw,ey*ch],[ex*cw,(ey+1)*ch]]);
    if(!S(ex+1,ey)) edges.push([[(ex+1)*cw,ey*ch],[(ex+1)*cw,(ey+1)*ch]]);
    if(!S(ex,ey-1)) edges.push([[ex*cw,ey*ch],[(ex+1)*cw,ey*ch]]);
    if(!S(ex,ey+1)) edges.push([[ex*cw,(ey+1)*ch],[(ex+1)*cw,(ey+1)*ch]]); }
  return edges; }
function exportSVG(){ const SC=exportScale(); if(!SC){ $('f2dInfo').textContent='Erst rechnen (▶ SIMP / ⚡ SKO).'; return; }
  const edges=boundaryEdges(SC), mx=p=>((p[0]-SC.bx0)*SC.k).toFixed(3), my=p=>((p[1]-SC.by0)*SC.k).toFixed(3);
  const w=((SC.bx1-SC.bx0)*SC.k).toFixed(2), h=((SC.by1-SC.by0)*SC.k).toFixed(2); let d='';
  for(const e of edges) d+='M'+mx(e[0])+' '+my(e[0])+' L'+mx(e[1])+' '+my(e[1])+' ';
  download('form2d.svg','<svg xmlns="http://www.w3.org/2000/svg" width="'+w+'mm" height="'+h+'mm" viewBox="0 0 '+w+' '+h+'"><path d="'+d+'" fill="none" stroke="#000" stroke-width="0.3"/></svg>','image/svg+xml');
  $('f2dInfo').textContent='SVG exportiert ('+w+'×'+h+' mm).'; }
function exportDXF(){ const SC=exportScale(); if(!SC){ $('f2dInfo').textContent='Erst rechnen (▶ SIMP / ⚡ SKO).'; return; }
  const edges=boundaryEdges(SC), mx=p=>((p[0]-SC.bx0)*SC.k), my=p=>((SC.by1-p[1])*SC.k);
  let e='0\nSECTION\n2\nENTITIES\n'; for(const ed of edges) e+='0\nLINE\n8\n0\n10\n'+mx(ed[0]).toFixed(3)+'\n20\n'+my(ed[0]).toFixed(3)+'\n11\n'+mx(ed[1]).toFixed(3)+'\n21\n'+my(ed[1]).toFixed(3)+'\n'; e+='0\nENDSEC\n0\nEOF\n';
  download('form2d.dxf',e,'application/dxf'); $('f2dInfo').textContent='DXF exportiert.'; }
function exportSTL(){ const SC=exportScale(); if(!SC){ $('f2dInfo').textContent='Erst rechnen (▶ SIMP / ⚡ SKO).'; return; } const thick=Math.max(0.2,+($('f2dExT').value||5));
  const s=SC.s, cw=SC.cw, ch=SC.ch, k=SC.k, bx0=SC.bx0, by1=SC.by1, S=(ex,ey)=>(ex<0||ey<0||ex>=nelx||ey>=nely)?0:s[ex*nely+ey];
  const X=px=>(px-bx0)*k, Y=py=>(by1-py)*k, tris=[], q=(a,b,c,d)=>{ tris.push(a,b,c); tris.push(a,c,d); };
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ if(!s[ex*nely+ey])continue;
    const x0=X(ex*cw), x1=X((ex+1)*cw), y0=Y((ey+1)*ch), y1=Y(ey*ch);
    q([x0,y0,0],[x1,y0,0],[x1,y1,0],[x0,y1,0]); q([x0,y0,thick],[x0,y1,thick],[x1,y1,thick],[x1,y0,thick]);
    if(!S(ex-1,ey)) q([x0,y0,0],[x0,y1,0],[x0,y1,thick],[x0,y0,thick]);
    if(!S(ex+1,ey)) q([x1,y1,0],[x1,y0,0],[x1,y0,thick],[x1,y1,thick]);
    if(!S(ex,ey-1)) q([x0,y1,0],[x1,y1,0],[x1,y1,thick],[x0,y1,thick]);
    if(!S(ex,ey+1)) q([x1,y0,0],[x0,y0,0],[x0,y0,thick],[x1,y0,thick]); }
  let t='solid form2d\n'; for(let i=0;i<tris.length;i+=3){ const a=tris[i],b=tris[i+1],c=tris[i+2],
    nx=(b[1]-a[1])*(c[2]-a[2])-(b[2]-a[2])*(c[1]-a[1]), ny=(b[2]-a[2])*(c[0]-a[0])-(b[0]-a[0])*(c[2]-a[2]), nz=(b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0]), nl=Math.hypot(nx,ny,nz)||1;
    t+='facet normal '+(nx/nl).toFixed(4)+' '+(ny/nl).toFixed(4)+' '+(nz/nl).toFixed(4)+'\nouter loop\nvertex '+a[0].toFixed(3)+' '+a[1].toFixed(3)+' '+a[2].toFixed(3)+'\nvertex '+b[0].toFixed(3)+' '+b[1].toFixed(3)+' '+b[2].toFixed(3)+'\nvertex '+c[0].toFixed(3)+' '+c[1].toFixed(3)+' '+c[2].toFixed(3)+'\nendloop\nendfacet\n'; }
  t+='endsolid form2d\n'; download('form2d.stl',t,'model/stl'); $('f2dInfo').textContent='STL exportiert ('+(tris.length/3)+' Dreiecke, '+thick+' mm dick).'; }
window.Form2DUI={init,show};
if(document.readyState!=='loading') init(); else document.addEventListener('DOMContentLoaded',init);
})();
