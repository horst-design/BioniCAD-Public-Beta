// Voxel-FEM (HEX8) mit matrixfreiem CG-Löser — validierter Kern fürs Mattheck/SKO-Modul.
// Lineare Elastizität, statisch, ein Material. Einheiten empfohlen: mm, N, MPa (N/mm²).
// Validiert gegen Kragträger (Durchbiegung ~0.97·Euler; von-Mises-Peak an der Einspannung).
(function(global){
  const S3=1/Math.sqrt(3), GP=[-S3,S3];
  const XI=[[-1,-1,-1],[1,-1,-1],[1,1,-1],[-1,1,-1],[-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1]];
  function Dmat(E,nu){ const f=E/((1+nu)*(1-2*nu)),a=1-nu,b=nu,c=(1-2*nu)/2;
    return [[f*a,f*b,f*b,0,0,0],[f*b,f*a,f*b,0,0,0],[f*b,f*b,f*a,0,0,0],[0,0,0,f*c,0,0],[0,0,0,0,f*c,0],[0,0,0,0,0,f*c]]; }
  function Bmat(xi,eta,ze,h){ const inv=2/h, B=Array.from({length:6},()=>new Float64Array(24));
    for(let i=0;i<8;i++){ const s=XI[i];
      const dNx=0.125*s[0]*(1+eta*s[1])*(1+ze*s[2])*inv, dNy=0.125*s[1]*(1+xi*s[0])*(1+ze*s[2])*inv, dNz=0.125*s[2]*(1+xi*s[0])*(1+eta*s[1])*inv, c=3*i;
      B[0][c]=dNx; B[1][c+1]=dNy; B[2][c+2]=dNz; B[3][c]=dNy; B[3][c+1]=dNx; B[4][c+1]=dNz; B[4][c+2]=dNy; B[5][c]=dNz; B[5][c+2]=dNx; }
    return B; }
  function elementK(E,nu,h){ const D=Dmat(E,nu), Ke=Array.from({length:24},()=>new Float64Array(24)), detJ=(h/2)**3;
    for(const xi of GP)for(const eta of GP)for(const ze of GP){ const B=Bmat(xi,eta,ze,h), DB=Array.from({length:6},()=>new Float64Array(24));
      for(let r=0;r<6;r++)for(let cc=0;cc<24;cc++){ let s=0; for(let k=0;k<6;k++) s+=D[r][k]*B[k][cc]; DB[r][cc]=s; }
      for(let a=0;a<24;a++)for(let bb=0;bb<24;bb++){ let s=0; for(let k=0;k<6;k++) s+=B[k][a]*DB[k][bb]; Ke[a][bb]+=s*detJ; } }
    return Ke; }
  // Eigenzerlegung einer symmetrischen 3x3-Matrix (zyklisches Jacobi). m=[xx,yy,zz,xy,xz,yz].
  // Rückgabe: {val:[l0,l1,l2], vec:[v0,v1,v2]} mit Einheits-Eigenvektoren vi (zu val[i]).
  function eig3sym(m){
    const a=[[m[0],m[3],m[4]],[m[3],m[1],m[5]],[m[4],m[5],m[2]]];
    const v=[[1,0,0],[0,1,0],[0,0,1]];
    for(let it=0; it<50; it++){
      const off=Math.abs(a[0][1])+Math.abs(a[0][2])+Math.abs(a[1][2]);
      if(off<1e-14) break;
      for(let p=0;p<2;p++)for(let q=p+1;q<3;q++){
        if(Math.abs(a[p][q])<1e-20) continue;
        const th=(a[q][q]-a[p][p])/(2*a[p][q]);
        const t=(th===0)?1:(Math.sign(th)/(Math.abs(th)+Math.sqrt(th*th+1)));
        const c=1/Math.sqrt(t*t+1), s=t*c, apq=a[p][q];
        a[p][p]-=t*apq; a[q][q]+=t*apq; a[p][q]=a[q][p]=0;
        for(let k=0;k<3;k++){ if(k!==p&&k!==q){ const akp=a[k][p], akq=a[k][q]; a[k][p]=a[p][k]=c*akp-s*akq; a[k][q]=a[q][k]=s*akp+c*akq; } }
        for(let k=0;k<3;k++){ const vkp=v[k][p], vkq=v[k][q]; v[k][p]=c*vkp-s*vkq; v[k][q]=s*vkp+c*vkq; }
      }
    }
    return { val:[a[0][0],a[1][1],a[2][2]],
             vec:[[v[0][0],v[1][0],v[2][0]],[v[0][1],v[1][1],v[2][1]],[v[0][2],v[1][2],v[2][2]]] };
  }
  // opts: {nx,ny,nz,h,E,nu, active:(i,j,k)=>bool, isFixed:(node)=>bool, forces:[[node,[fx,fy,fz]]], maxIter, tol}
  function solve(opts){
    const {nx,ny,nz,h,E,nu}=opts, NX=nx+1,NY=ny+1,NZ=nz+1, nN=NX*NY*NZ, nD=3*nN;
    const nid=(i,j,k)=>(k*NY+j)*NX+i;
    const active=opts.active||(()=>true), dens=opts.dens||(()=>1), Ke=elementK(E,nu,h), elems=[], eijk=[];
    const nodeUsed=new Uint8Array(nN);
    for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){ if(!active(i,j,k))continue;
      const el=[nid(i,j,k),nid(i+1,j,k),nid(i+1,j+1,k),nid(i,j+1,k),nid(i,j,k+1),nid(i+1,j,k+1),nid(i+1,j+1,k+1),nid(i,j+1,k+1)];
      elems.push(el); eijk.push([i,j,k]); for(const n of el) nodeUsed[n]=1; }
    const de=new Float64Array(elems.length); for(let e=0;e<elems.length;e++) de[e]=Math.max(dens(eijk[e][0],eijk[e][1],eijk[e][2]),1e-6);
    const fixed=new Uint8Array(nD), isFixed=opts.isFixed||(()=>false), fdof=opts.fixedDof||null;
    for(let n=0;n<nN;n++){ if(!nodeUsed[n]){ fixed[3*n]=fixed[3*n+1]=fixed[3*n+2]=1; continue; }
      if(fdof){ if(fdof[3*n])fixed[3*n]=1; if(fdof[3*n+1])fixed[3*n+1]=1; if(fdof[3*n+2])fixed[3*n+2]=1; }   // per-DOF (Roller/Pinned)
      else if(isFixed(n)){ fixed[3*n]=fixed[3*n+1]=fixed[3*n+2]=1; } }
    const f=new Float64Array(nD); for(const [n,v] of (opts.forces||[])){ f[3*n]+=v[0];f[3*n+1]+=v[1];f[3*n+2]+=v[2]; }
    for(let d=0;d<nD;d++) if(fixed[d]) f[d]=0;
    const ue=new Float64Array(24), keu=new Float64Array(24);
    function matvec(u,out){ out.fill(0);
      for(let e=0;e<elems.length;e++){ const el=elems[e], dd=de[e];
        for(let a=0;a<8;a++){const n=el[a];ue[3*a]=u[3*n];ue[3*a+1]=u[3*n+1];ue[3*a+2]=u[3*n+2];}
        for(let a=0;a<24;a++){ let s=0; const Ka=Ke[a]; for(let b=0;b<24;b++) s+=Ka[b]*ue[b]; keu[a]=s*dd; }
        for(let a=0;a<8;a++){const n=el[a];out[3*n]+=keu[3*a];out[3*n+1]+=keu[3*a+1];out[3*n+2]+=keu[3*a+2];} }
      for(let d=0;d<nD;d++) if(fixed[d]) out[d]=u[d]; }
    // Jacobi-Vorkonditionierer: Diagonale von K (dichte-skaliert)
    const diag=new Float32Array(nD);   // Float32: halbe Bandbreite im Matvec/CG (Dots bleiben double)
    for(let e=0;e<elems.length;e++){ const el=elems[e], dd=de[e];
      for(let a=0;a<8;a++){ const n=el[a]; diag[3*n]+=Ke[3*a][3*a]*dd; diag[3*n+1]+=Ke[3*a+1][3*a+1]*dd; diag[3*n+2]+=Ke[3*a+2][3*a+2]*dd; } }
    for(let d=0;d<nD;d++){ if(fixed[d]||!(diag[d]>0)) diag[d]=1; }
    const u=new Float32Array(nD), r=new Float32Array(nD), z=new Float32Array(nD), p=new Float32Array(nD), Ap=new Float32Array(nD);
    if(opts.u0 && opts.u0.length===nD){ u.set(opts.u0); for(let d=0;d<nD;d++) if(fixed[d]) u[d]=0; }   // Warmstart (SKO)
    matvec(u,Ap); let rs=0,rz=0; for(let d=0;d<nD;d++){ r[d]=f[d]-Ap[d]; z[d]=r[d]/diag[d]; p[d]=z[d]; rs+=r[d]*r[d]; rz+=r[d]*z[d]; }
    const tol=(opts.tol||1e-8)**2*Math.max(rs,1e-30); let it=0;
    for(const mi=opts.maxIter||10000; it<mi && rs>tol; it++){ matvec(p,Ap); let pAp=0; for(let d=0;d<nD;d++) pAp+=p[d]*Ap[d];
      if(Math.abs(pAp)<1e-30)break; const al=rz/pAp; let rz2=0; rs=0;
      for(let d=0;d<nD;d++){ u[d]+=al*p[d]; r[d]-=al*Ap[d]; z[d]=r[d]/diag[d]; rz2+=r[d]*z[d]; rs+=r[d]*r[d]; }
      const be=rz2/rz; for(let d=0;d<nD;d++) p[d]=z[d]+be*p[d]; rz=rz2; }
    // von-Mises je aktivem Element (Zentrum)
    const D=Dmat(E,nu), B0=Bmat(0,0,0,h), vm=[];
    for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){ if(!active(i,j,k)){continue;}
      const el=[nid(i,j,k),nid(i+1,j,k),nid(i+1,j+1,k),nid(i,j+1,k),nid(i,j,k+1),nid(i+1,j,k+1),nid(i+1,j+1,k+1),nid(i,j+1,k+1)];
      for(let a=0;a<8;a++){const n=el[a];ue[3*a]=u[3*n];ue[3*a+1]=u[3*n+1];ue[3*a+2]=u[3*n+2];}
      const eps=new Float64Array(6); for(let rr=0;rr<6;rr++){let s=0;for(let c=0;c<24;c++)s+=B0[rr][c]*ue[c];eps[rr]=s;}
      const sg=new Float64Array(6); for(let rr=0;rr<6;rr++){let s=0;for(let c=0;c<6;c++)s+=D[rr][c]*eps[c];sg[rr]=s;}
      const [sx,sy,sz,txy,tyz,tzx]=sg, v=Math.sqrt(0.5*((sx-sy)**2+(sy-sz)**2+(sz-sx)**2)+3*(txy*txy+tyz*tyz+tzx*tzx));
      let se=0; for(let a=0;a<24;a++){ let s=0; const Ka=Ke[a]; for(let b=0;b<24;b++) s+=Ka[b]*ue[b]; se+=ue[a]*s; }  // Dehnungsenergie (BESO)
      const eg=eig3sym([sx,sy,sz,txy,tzx,tyz]);   // Hauptspannungen: Werte + Richtungen
      vm.push({i,j,k,vm:v,se,pv:eg.val,pd:eg.vec}); }
    return {u, nid, vm, iter:it, resid:Math.sqrt(rs)};
  }
  global.FEM={solve, nid:(nx,ny,nz)=>{const NX=nx+1,NY=ny+1;return (i,j,k)=>(k*NY+j)*NX+i;}};
})(typeof window!=='undefined'?window:globalThis);
