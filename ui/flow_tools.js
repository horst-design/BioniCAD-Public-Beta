// Gemeinsame Kraftfluss-Editor-Logik für alle Module (Mattheck, Tessellation, Baum).
// Reine Datenoperationen auf Linien = [[[x,y,z],...], ...]; jedes Modul ruft sie aus
// seiner eigenen Interaktion (Picken/Gizmo/Klicks) auf. Kein Framework-Zwang.
(function(global){
  // Laplacian-Glättung einer Polylinie (Endpunkte fest).
  function smoothPolyline(L, iters, lambda){
    lambda = lambda==null?0.5:lambda; let P=L.map(p=>p.slice());
    for(let it=0; it<iters; it++){ const Q=P.map(p=>p.slice());
      for(let i=1;i<P.length-1;i++) for(let c=0;c<3;c++) Q[i][c]=P[i][c]+lambda*(0.5*(P[i-1][c]+P[i+1][c])-P[i][c]);
      P=Q; }
    return P;
  }
  // Alle Linien glätten; amount 0..1 -> Iterationen 0..6.
  function smooth(lines, amount){ const it=Math.round(Math.max(0,Math.min(1,amount))*6); if(it<=0) return lines.map(L=>L.map(p=>p.slice())); return lines.map(L=>smoothPolyline(L,it,0.5)); }
  // Linie um Vektor verschieben.
  function translateLine(L, d){ return L.map(p=>[p[0]+d[0],p[1]+d[1],p[2]+d[2]]); }
  // Schwerpunkt einer Linie.
  function centroid(L){ let x=0,y=0,z=0; for(const p of L){x+=p[0];y+=p[1];z+=p[2];} const n=L.length||1; return [x/n,y/n,z/n]; }
  // Nächste Linie zu einem Strahl (Origin o, Richtung d normiert). Rückgabe {idx,dist} oder null.
  function pickLine(lines, o, d, thr){ let best=-1, bd=Infinity;
    for(let i=0;i<lines.length;i++){ for(const p of lines[i]){ const ax=p[0]-o[0],ay=p[1]-o[1],az=p[2]-o[2];
      const t=ax*d[0]+ay*d[1]+az*d[2]; const ex=ax-t*d[0],ey=ay-t*d[1],ez=az-t*d[2]; const dist=Math.sqrt(ex*ex+ey*ey+ez*ez);
      if(dist<bd){bd=dist;best=i;} } }
    if(best<0) return null; if(thr!=null && bd>thr) return null; return {idx:best, dist:bd};
  }
  // Punkt auf eine Ebene (durch pivot, Normale = Kameravorwärts nView) projizieren — für „Linie zeichnen".
  // ro,rd = Strahl (Origin, Richtung). Rückgabe [x,y,z] oder null.
  function rayPlane(ro, rd, pivot, n){ const denom=rd[0]*n[0]+rd[1]*n[1]+rd[2]*n[2]; if(Math.abs(denom)<1e-9) return null;
    const t=((pivot[0]-ro[0])*n[0]+(pivot[1]-ro[1])*n[1]+(pivot[2]-ro[2])*n[2])/denom; if(t<0) return null;
    return [ro[0]+rd[0]*t, ro[1]+rd[1]*t, ro[2]+rd[2]*t];
  }
  // Polylinie nach Bogenlänge gleichmäßig neu abtasten (für sauberes Nachzeichnen).
  function resample(L, step){ if(L.length<2) return L.map(p=>p.slice()); const out=[L[0].slice()]; let acc=0;
    for(let i=1;i<L.length;i++){ let ax=L[i][0]-L[i-1][0],ay=L[i][1]-L[i-1][1],az=L[i][2]-L[i-1][2]; let seg=Math.sqrt(ax*ax+ay*ay+az*az); if(seg<1e-9)continue;
      let dir=[ax/seg,ay/seg,az/seg], start=acc, from=L[i-1];
      while(acc+ (step-(start%step||step)) <= start+seg){ /* noop */ break; }
      let d=step-(acc%step); if(acc===0)d=step;
      while(d<=seg){ out.push([from[0]+dir[0]*d, from[1]+dir[1]*d, from[2]+dir[2]*d]); d+=step; }
      acc+=seg; }
    out.push(L[L.length-1].slice()); return out;
  }
  global.FlowTools={ smoothPolyline, smooth, translateLine, centroid, pickLine, rayPlane, resample };
})(typeof window!=='undefined'?window:globalThis);
