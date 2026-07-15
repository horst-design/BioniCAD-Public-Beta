// Gemeinsame Boolean-Werkzeuge für alle Module (Mattheck, Tessellation, Baum).
// Schickt Resultat (A) + zweiten Körper (B) an den Kern-Endpunkt POST /boolean.
// Der Kern voxelisiert beide STLs und macht union/subtract/intersect wasserdicht.
(function(global){
  function ab2b64(ab){ const u=new Uint8Array(ab); let s=''; const c=0x8000;
    for(let i=0;i<u.length;i+=c) s+=String.fromCharCode.apply(null,u.subarray(i,i+c)); return btoa(s); }

  // Binäres STL aus Dreiecksliste [[[x,y,z],[..],[..]], ...]
  function stlBytesFromTris(tris){ const n=tris.length, buf=new ArrayBuffer(84+n*50), dv=new DataView(buf);
    dv.setUint32(80,n,true); let o=84;
    for(const t of tris){ const ux=t[1][0]-t[0][0],uy=t[1][1]-t[0][1],uz=t[1][2]-t[0][2],
        vx=t[2][0]-t[0][0],vy=t[2][1]-t[0][1],vz=t[2][2]-t[0][2];
      let nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx; const nl=Math.hypot(nx,ny,nz)||1; nx/=nl;ny/=nl;nz/=nl;
      dv.setFloat32(o,nx,true);dv.setFloat32(o+4,ny,true);dv.setFloat32(o+8,nz,true);o+=12;
      for(const p of t){ dv.setFloat32(o,p[0],true);dv.setFloat32(o+4,p[1],true);dv.setFloat32(o+8,p[2],true);o+=12; }
      dv.setUint16(o,0,true);o+=2; }
    return buf; }

  // Dreiecke aus einer THREE.BufferGeometry (World-unabhängig, lokale Koordinaten).
  function trisFromGeom(g){ const p=g.attributes.position,n=p.count,t=[];
    for(let i=0;i+2<n;i+=3) t.push([[p.getX(i),p.getY(i),p.getZ(i)],[p.getX(i+1),p.getY(i+1),p.getZ(i+1)],[p.getX(i+2),p.getY(i+2),p.getZ(i+2)]]);
    return t; }

  async function run(url, body){
    const u=(url||'http://localhost:5151/').replace(/\/*$/,'/')+'boolean';
    const r=await fetch(u,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(body)});
    if(!r.ok) throw new Error('Server '+r.status+': '+(await r.text()).slice(0,160));
    return await r.arrayBuffer();
  }

  // Baut ein Boolean-Panel in cfg.container und verdrahtet es.
  // cfg: { container, serverUrl():string, getResult():ArrayBuffer|null, getBuildTris():tris|null,
  //        voxel():number, onResult(ArrayBuffer buf, string label), warn(msg), info(msg) }
  function mountPanel(cfg){
    const c=cfg.container; if(!c) return;
    let impBuf=null, impName='';
    c.innerHTML =
      '<div style="display:flex;gap:8px;flex-wrap:wrap;align-items:flex-end">'
      + '<div style="flex:1;min-width:120px"><label style="display:block;font-size:12px;opacity:.8;margin-bottom:3px">Zweiter Körper (B)</label>'
      +   '<select class="bo-src" style="width:100%"><option value="bau">Bauraum-STL</option><option value="imp">STL importieren…</option></select></div>'
      + '<div style="flex:1;min-width:150px"><label style="display:block;font-size:12px;opacity:.8;margin-bottom:3px">Operation</label>'
      +   '<select class="bo-op" style="width:100%">'
      +     '<option value="union">Vereinen (Resultat ∪ B)</option>'
      +     '<option value="subtract">Abziehen (Resultat − B)</option>'
      +     '<option value="intersect">Split → innen behalten (∩)</option>'
      +     '<option value="subout">Split → außen behalten (−)</option>'
      +   '</select></div>'
      + '</div>'
      + '<div style="display:flex;gap:8px;align-items:center;margin-top:8px">'
      +   '<button class="bo-apply mini">⚙ Boolean anwenden</button>'
      +   '<span class="bo-status" style="font-size:12px;opacity:.75"></span>'
      + '</div>'
      + '<input type="file" class="bo-file" accept=".stl" style="display:none">';
    const q=s=>c.querySelector(s);
    const srcSel=q('.bo-src'), fileIn=q('.bo-file'), status=q('.bo-status'), applyBtn=q('.bo-apply');

    srcSel.addEventListener('change',()=>{ if(srcSel.value==='imp' && !impBuf) fileIn.click(); });
    fileIn.addEventListener('change',e=>{ const f=e.target.files&&e.target.files[0]; if(!f){ srcSel.value='bau'; return; }
      const rd=new FileReader(); rd.onload=ev=>{ impBuf=ev.target.result; impName=f.name; status.textContent='Import: '+f.name; srcSel.value='imp'; };
      rd.readAsArrayBuffer(f); e.target.value=''; });

    applyBtn.addEventListener('click', async ()=>{
      try{
        const A=cfg.getResult&&cfg.getResult();
        if(!A){ (cfg.warn||console.warn)('Erst ein Kern-Resultat erzeugen (Kern rendern), dann Boolean.'); return; }
        let Bbytes=null, blabel='';
        if(srcSel.value==='imp'){
          if(!impBuf){ fileIn.click(); return; }
          Bbytes=impBuf; blabel=impName;
        } else {
          const tris=cfg.getBuildTris&&cfg.getBuildTris();
          if(!tris||!tris.length){ (cfg.warn||console.warn)('Keine Bauraum-STL geladen — importiere eine STL oder lade den Bauraum.'); return; }
          Bbytes=stlBytesFromTris(tris); blabel='Bauraum';
        }
        const opRaw=q('.bo-op').value, op=(opRaw==='subout')?'subtract':opRaw;
        applyBtn.disabled=true; const old=applyBtn.textContent; applyBtn.textContent='⏳ Kern…'; status.textContent='';
        const body={ A:ab2b64(A), B:ab2b64(Bbytes), op, voxel:(cfg.voxel&&cfg.voxel())||0.3, fillet:0 };
        const buf=await run(cfg.serverUrl&&cfg.serverUrl(), body);
        const kb=(buf.byteLength/1024).toFixed(0);
        cfg.onResult && cfg.onResult(buf, opRaw+' '+blabel);
        status.textContent='✓ '+opRaw+' ('+kb+' KB)';
        applyBtn.textContent=old; applyBtn.disabled=false;
      }catch(err){ (cfg.warn||console.warn)('Boolean-Fehler: '+err.message); status.textContent='⚠ '+err.message; applyBtn.textContent='⚙ Boolean anwenden'; applyBtn.disabled=false; }
    });
  }

  global.BoolOps={ ab2b64, stlBytesFromTris, trisFromGeom, run, mountPanel };
})(window);
