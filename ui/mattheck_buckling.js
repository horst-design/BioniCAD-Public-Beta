// Lineare Beulanalyse (Methode 3a) — geometrische Steifigkeit Kσ + inverse Iteration.
// Eigenständig (eigener PCG als K^-1); der validierte FE-Kern mattheck_fem.js bleibt unberührt.
// Verifiziert gegen Euler-Knicklast (FE/Euler ~1.06, Skalierung P_cr ∝ 1/L² exakt).
// Ergebnis: niedrigster Buckling-Load-Factor (BLF = kritischer Lastfaktor) + Knickmode.
(function(global){
  const XI=[[-1,-1,-1],[1,-1,-1],[1,1,-1],[-1,1,-1],[-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1]];
  const S3=1/Math.sqrt(3), GP=[-S3,S3];
  function Dmat(E,nu){const f=E/((1+nu)*(1-2*nu)),a=1-nu,b=nu,c=(1-2*nu)/2;
    return [[f*a,f*b,f*b,0,0,0],[f*b,f*a,f*b,0,0,0],[f*b,f*b,f*a,0,0,0],[0,0,0,f*c,0,0],[0,0,0,0,f*c,0],[0,0,0,0,0,f*c]];}
  function grads(xi,eta,ze,h){const g=[],inv=2/h;
    for(let a=0;a<8;a++){const s=XI[a];
      g.push([0.125*s[0]*(1+eta*s[1])*(1+ze*s[2])*inv,0.125*s[1]*(1+xi*s[0])*(1+ze*s[2])*inv,0.125*s[2]*(1+xi*s[0])*(1+eta*s[1])*inv]);}
    return g;}
  function Bfromg(g){const B=Array.from({length:6},()=>new Float64Array(24));
    for(let a=0;a<8;a++){const c=3*a,gx=g[a][0],gy=g[a][1],gz=g[a][2];
      B[0][c]=gx;B[1][c+1]=gy;B[2][c+2]=gz;B[3][c]=gy;B[3][c+1]=gx;B[4][c+1]=gz;B[4][c+2]=gy;B[5][c]=gz;B[5][c+2]=gx;}return B;}
  function elementK(E,nu,h){const D=Dmat(E,nu),Ke=Array.from({length:24},()=>new Float64Array(24)),detJ=(h/2)**3;
    for(const xi of GP)for(const eta of GP)for(const ze of GP){const B=Bfromg(grads(xi,eta,ze,h)),DB=Array.from({length:6},()=>new Float64Array(24));
      for(let r=0;r<6;r++)for(let cc=0;cc<24;cc++){let s=0;for(let k=0;k<6;k++)s+=D[r][k]*B[k][cc];DB[r][cc]=s;}
      for(let i=0;i<24;i++)for(let j=0;j<24;j++){let s=0;for(let k=0;k<6;k++)s+=B[k][i]*DB[k][j];Ke[i][j]+=s*detJ;}}return Ke;}
  function gpGrads(h){const out=[],detJ=(h/2)**3;for(const xi of GP)for(const eta of GP)for(const ze of GP)out.push({g:grads(xi,eta,ze,h),detJ});return out;}

  // opts: {nx,ny,nz,h,E,nu, active:(i,j,k)=>bool, isFixed:(node)=>bool, forces:[[node,[fx,fy,fz]]], dens:(i,j,k)=>0..1, maxEig, tol}
  function analyze(opts){
    const {nx,ny,nz,h,E,nu}=opts, NX=nx+1,NY=ny+1,NZ=nz+1, nN=NX*NY*NZ, nD=3*nN;
    const nid=(i,j,k)=>(k*NY+j)*NX+i;
    const active=opts.active||(()=>true), densf=opts.dens||(()=>1);
    const Ke=elementK(E,nu,h), D=Dmat(E,nu), gpg=gpGrads(h), B0=Bfromg(grads(0,0,0,h));
    const elems=[], eijk=[], de=[]; const nodeUsed=new Uint8Array(nN);
    for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){ if(!active(i,j,k))continue;
      const el=[nid(i,j,k),nid(i+1,j,k),nid(i+1,j+1,k),nid(i,j+1,k),nid(i,j,k+1),nid(i+1,j,k+1),nid(i+1,j+1,k+1),nid(i,j+1,k+1)];
      elems.push(el); eijk.push([i,j,k]); const r=Math.max(densf(i,j,k),1e-6); de.push(r*r*r); for(const n of el) nodeUsed[n]=1; }
    const nel=elems.length;
    const fixed=new Uint8Array(nD), isFixed=opts.isFixed||(()=>false), fdof=opts.fixedDof||null;
    for(let n=0;n<nN;n++){ if(!nodeUsed[n]){ fixed[3*n]=fixed[3*n+1]=fixed[3*n+2]=1; continue; }
      if(fdof){ if(fdof[3*n])fixed[3*n]=1; if(fdof[3*n+1])fixed[3*n+1]=1; if(fdof[3*n+2])fixed[3*n+2]=1; }
      else if(isFixed(n)){ fixed[3*n]=fixed[3*n+1]=fixed[3*n+2]=1; } }
    const f=new Float64Array(nD); for(const [n,v] of (opts.forces||[])){ f[3*n]+=v[0];f[3*n+1]+=v[1];f[3*n+2]+=v[2]; }
    for(let d=0;d<nD;d++) if(fixed[d]) f[d]=0;
    const ue=new Float64Array(24), tmp=new Float64Array(24);
    function matvecK(u,out){ out.fill(0);
      for(let e=0;e<nel;e++){ const el=elems[e], dd=de[e];
        for(let a=0;a<8;a++){const n=el[a];ue[3*a]=u[3*n];ue[3*a+1]=u[3*n+1];ue[3*a+2]=u[3*n+2];}
        for(let a=0;a<24;a++){let s=0;const Ka=Ke[a];for(let b=0;b<24;b++)s+=Ka[b]*ue[b];tmp[a]=s*dd;}
        for(let a=0;a<8;a++){const n=el[a];out[3*n]+=tmp[3*a];out[3*n+1]+=tmp[3*a+1];out[3*n+2]+=tmp[3*a+2];} }
      for(let d=0;d<nD;d++) if(fixed[d]) out[d]=u[d]; }
    const diag=new Float64Array(nD);
    for(let e=0;e<nel;e++){ const el=elems[e], dd=de[e]; for(let a=0;a<8;a++){const n=el[a];diag[3*n]+=Ke[3*a][3*a]*dd;diag[3*n+1]+=Ke[3*a+1][3*a+1]*dd;diag[3*n+2]+=Ke[3*a+2][3*a+2]*dd;} }
    for(let d=0;d<nD;d++){ if(fixed[d]||!(diag[d]>0)) diag[d]=1; }
    function pcg(rhs,tol,maxit){ const u=new Float64Array(nD),r=new Float64Array(nD),z=new Float64Array(nD),p=new Float64Array(nD),Ap=new Float64Array(nD);
      matvecK(u,Ap); let rz=0,rs=0; for(let d=0;d<nD;d++){r[d]=rhs[d]-Ap[d];z[d]=r[d]/diag[d];p[d]=z[d];rz+=r[d]*z[d];rs+=r[d]*r[d];}
      const tol2=tol*tol*Math.max(rs,1e-30); let it=0; for(;it<maxit&&rs>tol2;it++){ matvecK(p,Ap); let pAp=0; for(let d=0;d<nD;d++)pAp+=p[d]*Ap[d];
        if(Math.abs(pAp)<1e-300)break; const al=rz/pAp; let rz2=0; rs=0; for(let d=0;d<nD;d++){u[d]+=al*p[d];r[d]-=al*Ap[d];z[d]=r[d]/diag[d];rz2+=r[d]*z[d];rs+=r[d]*r[d];}
        const be=rz2/rz; for(let d=0;d<nD;d++)p[d]=z[d]+be*p[d]; rz=rz2; } return u; }
    // 1) Vorbeul-Zustand
    const u0=pcg(f, opts.tol||1e-7, opts.maxCG||5000);
    // 2) Elementspannungen (Zentrum), dichteskaliert  s=[sxx,syy,szz,sxy,syz,szx]
    const S=new Array(nel);
    for(let e=0;e<nel;e++){ const el=elems[e], dd=de[e]; for(let a=0;a<8;a++){const n=el[a];ue[3*a]=u0[3*n];ue[3*a+1]=u0[3*n+1];ue[3*a+2]=u0[3*n+2];}
      const eps=new Float64Array(6); for(let r=0;r<6;r++){let s=0;for(let c=0;c<24;c++)s+=B0[r][c]*ue[c];eps[r]=s;}
      const sg=new Float64Array(6); for(let r=0;r<6;r++){let s=0;for(let c=0;c<6;c++)s+=D[r][c]*eps[c];sg[r]=s*dd;} S[e]=sg; }
    // 3) Kσ-matvec
    function matvecKg(u,out){ out.fill(0);
      for(let e=0;e<nel;e++){ const el=elems[e], s=S[e]; const sig=[[s[0],s[3],s[5]],[s[3],s[1],s[4]],[s[5],s[4],s[2]]];
        for(let a=0;a<8;a++){ue[3*a]=u[3*el[a]];ue[3*a+1]=u[3*el[a]+1];ue[3*a+2]=u[3*el[a]+2];}
        const acc=new Float64Array(24);
        for(const gp of gpg){ const g=gp.g, dJ=gp.detJ, sgb=[];
          for(let b=0;b<8;b++){const gb=g[b];sgb.push([sig[0][0]*gb[0]+sig[0][1]*gb[1]+sig[0][2]*gb[2],sig[1][0]*gb[0]+sig[1][1]*gb[1]+sig[1][2]*gb[2],sig[2][0]*gb[0]+sig[2][1]*gb[1]+sig[2][2]*gb[2]]);}
          for(let a=0;a<8;a++){const ga=g[a];
            for(let b=0;b<8;b++){const Mab=(ga[0]*sgb[b][0]+ga[1]*sgb[b][1]+ga[2]*sgb[b][2])*dJ;
              acc[3*a]+=Mab*ue[3*b];acc[3*a+1]+=Mab*ue[3*b+1];acc[3*a+2]+=Mab*ue[3*b+2];}} }
        for(let a=0;a<8;a++){const n=el[a];out[3*n]+=acc[3*a];out[3*n+1]+=acc[3*a+1];out[3*n+2]+=acc[3*a+2];} }
      for(let d=0;d<nD;d++) if(fixed[d]) out[d]=0; }
    // 4) Inverse Iteration -> kleinster BLF + Mode
    let phi=new Float64Array(nD); let seed=12345; const rnd=()=>{ seed=(seed*1103515245+12345)&0x7fffffff; return seed/0x7fffffff-0.5; };
    for(let d=0;d<nD;d++) phi[d]=fixed[d]?0:rnd();
    const Kg=new Float64Array(nD), Kp=new Float64Array(nD); let lam=0, lamPrev=0, it=0;
    const maxEig=opts.maxEig||40;
    for(; it<maxEig; it++){ matvecKg(phi,Kg); for(let d=0;d<nD;d++) Kg[d]=-Kg[d];
      const psi=pcg(Kg, opts.tol||1e-7, opts.maxCG||5000); let nrm=0; for(let d=0;d<nD;d++)nrm+=psi[d]*psi[d]; nrm=Math.sqrt(nrm)||1;
      for(let d=0;d<nD;d++) phi[d]=psi[d]/nrm;
      matvecK(phi,Kp); matvecKg(phi,Kg); let a=0,b=0; for(let d=0;d<nD;d++){a+=phi[d]*Kp[d];b+=phi[d]*Kg[d];}
      lam=-a/b; if(it>2 && Math.abs(lam-lamPrev)<1e-4*Math.abs(lam)) { it++; break; } lamPrev=lam; }
    return { blf:lam, mode:Float32Array.from(phi), iter:it, nD };
  }

  // ===== Methode 3b: Beul-OPTIMIERUNG (max. niedrigster BLF bei Volumen-Constraint) =====
  // Multi-Mode-Eigenlöser (Guard-Vektoren + PCG-Warmstart), KS-Aggregation, verifizierte
  // BLF-Sensitivität (Adjungierte), OC-Update + Dichtefilter. Druckdominierte Fälle.
  // opts: {nx,ny,nz,h,E,nu, isFixed, forces, protect:(i,j,k)=>bool, volfrac, iters, move,
  //        rmin, kModes, guard, ksP, onIter(it,blf)}
  function optimize(opts){
    const {nx,ny,nz,h,E,nu}=opts, NX=nx+1,NY=ny+1,NZ=nz+1, nN=NX*NY*NZ, nD=3*nN, nel=nx*ny*nz;
    const nid=(i,j,k)=>(k*NY+j)*NX+i, eidx=(i,j,k)=>(k*ny+j)*nx+i;
    const D=Dmat(E,nu), Ke=elementK(E,nu,h), B0=Bfromg(grads(0,0,0,h)), gpg=gpGrads(h);
    const isFixed=opts.isFixed||(()=>false), protect=opts.protect||(()=>false);
    const vf=Math.min(Math.max(opts.volfrac||0.5,0.05),0.95), iters=opts.iters||40;
    const move=opts.move||0.1, rmin=Math.max(opts.rmin||1.5,1.0);
    const kM=opts.kModes||4, guard=opts.guard||3, ksP=opts.ksP||18;
    const elems=[]; for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++) elems.push([nid(i,j,k),nid(i+1,j,k),nid(i+1,j+1,k),nid(i,j+1,k),nid(i,j,k+1),nid(i+1,j,k+1),nid(i+1,j+1,k+1),nid(i,j+1,k+1)]);
    const prot=new Uint8Array(nel); for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++) if(protect(i,j,k)) prot[eidx(i,j,k)]=1;
    const fixed=new Uint8Array(nD), fdof=opts.fixedDof||null; for(let n=0;n<nN;n++){ if(fdof){ if(fdof[3*n])fixed[3*n]=1; if(fdof[3*n+1])fixed[3*n+1]=1; if(fdof[3*n+2])fixed[3*n+2]=1; } else if(isFixed(n)){ fixed[3*n]=fixed[3*n+1]=fixed[3*n+2]=1; } }
    const f=new Float64Array(nD); for(const [n,v] of (opts.forces||[])){ f[3*n]+=v[0];f[3*n+1]+=v[1];f[3*n+2]+=v[2]; } for(let d=0;d<nD;d++) if(fixed[d]) f[d]=0;
    const uw=new Float64Array(24), tw=new Float64Array(24);
    function mvK(de,u,o){ o.fill(0); for(let e=0;e<nel;e++){ const el=elems[e],dd=de[e]; for(let a=0;a<8;a++){const n=el[a];uw[3*a]=u[3*n];uw[3*a+1]=u[3*n+1];uw[3*a+2]=u[3*n+2];} for(let a=0;a<24;a++){let s=0;const Ka=Ke[a];for(let b=0;b<24;b++)s+=Ka[b]*uw[b];tw[a]=s*dd;} for(let a=0;a<8;a++){const n=el[a];o[3*n]+=tw[3*a];o[3*n+1]+=tw[3*a+1];o[3*n+2]+=tw[3*a+2];} } for(let d=0;d<nD;d++) if(fixed[d]) o[d]=u[d]; }
    function diagOf(de){ const g=new Float64Array(nD); for(let e=0;e<nel;e++){const el=elems[e],dd=de[e];for(let a=0;a<8;a++){const n=el[a];g[3*n]+=Ke[3*a][3*a]*dd;g[3*n+1]+=Ke[3*a+1][3*a+1]*dd;g[3*n+2]+=Ke[3*a+2][3*a+2]*dd;}} for(let d=0;d<nD;d++){if(fixed[d]||!(g[d]>0))g[d]=1;} return g; }
    function pcg(de,dg,rhs,tol,mx,x0){ const u=x0?x0.slice():new Float64Array(nD),r=new Float64Array(nD),z=new Float64Array(nD),p=new Float64Array(nD),Ap=new Float64Array(nD); if(x0)for(let d=0;d<nD;d++)if(fixed[d])u[d]=0; mvK(de,u,Ap); let rz=0,rs=0,n0=0; for(let d=0;d<nD;d++){r[d]=rhs[d]-Ap[d];z[d]=r[d]/dg[d];p[d]=z[d];rz+=r[d]*z[d];rs+=r[d]*r[d];n0+=rhs[d]*rhs[d];} const tol2=tol*tol*Math.max(n0,1e-30); for(let it=0;it<mx&&rs>tol2;it++){ mvK(de,p,Ap); let pAp=0;for(let d=0;d<nD;d++)pAp+=p[d]*Ap[d]; if(Math.abs(pAp)<1e-300)break; const al=rz/pAp; let rz2=0;rs=0; for(let d=0;d<nD;d++){u[d]+=al*p[d];r[d]-=al*Ap[d];z[d]=r[d]/dg[d];rz2+=r[d]*z[d];rs+=r[d]*r[d];} const be=rz2/rz;for(let d=0;d<nD;d++)p[d]=z[d]+be*p[d];rz=rz2; } return u; }
    function sig0(u){ const S=new Array(nel); for(let e=0;e<nel;e++){const el=elems[e];for(let a=0;a<8;a++){const n=el[a];uw[3*a]=u[3*n];uw[3*a+1]=u[3*n+1];uw[3*a+2]=u[3*n+2];}const ep=new Float64Array(6);for(let r=0;r<6;r++){let s=0;for(let c=0;c<24;c++)s+=B0[r][c]*uw[c];ep[r]=s;}const sg=new Float64Array(6);for(let r=0;r<6;r++){let s=0;for(let c=0;c<6;c++)s+=D[r][c]*ep[c];sg[r]=s;}S[e]=sg;} return S; }
    function mvKg(de,S,u,o){ o.fill(0); for(let e=0;e<nel;e++){const el=elems[e],s=S[e],dd=de[e];const sig=[[s[0],s[3],s[5]],[s[3],s[1],s[4]],[s[5],s[4],s[2]]];for(let a=0;a<8;a++){uw[3*a]=u[3*el[a]];uw[3*a+1]=u[3*el[a]+1];uw[3*a+2]=u[3*el[a]+2];}const ac=new Float64Array(24);for(const gp of gpg){const g=gp.g,dJ=gp.detJ*dd,sb=[];for(let b=0;b<8;b++){const gb=g[b];sb.push([sig[0][0]*gb[0]+sig[0][1]*gb[1]+sig[0][2]*gb[2],sig[1][0]*gb[0]+sig[1][1]*gb[1]+sig[1][2]*gb[2],sig[2][0]*gb[0]+sig[2][1]*gb[1]+sig[2][2]*gb[2]]);}for(let a=0;a<8;a++){const ga=g[a];for(let b=0;b<8;b++){const M=(ga[0]*sb[b][0]+ga[1]*sb[b][1]+ga[2]*sb[b][2])*dJ;ac[3*a]+=M*uw[3*b];ac[3*a+1]+=M*uw[3*b+1];ac[3*a+2]+=M*uw[3*b+2];}}}for(let a=0;a<8;a++){const n=el[a];o[3*n]+=ac[3*a];o[3*n+1]+=ac[3*a+1];o[3*n+2]+=ac[3*a+2];}} for(let d=0;d<nD;d++)if(fixed[d])o[d]=0; }
    function jac(A,k){const V=Array.from({length:k},(_,i)=>Array.from({length:k},(_,j)=>i===j?1:0));for(let sw=0;sw<100;sw++){let off=0;for(let p=0;p<k;p++)for(let q=p+1;q<k;q++)off+=A[p][q]*A[p][q];if(off<1e-26)break;for(let p=0;p<k;p++)for(let q=p+1;q<k;q++){if(Math.abs(A[p][q])<1e-30)continue;const th=(A[q][q]-A[p][p])/(2*A[p][q]),t=Math.sign(th||1)/(Math.abs(th)+Math.sqrt(th*th+1)),c=1/Math.sqrt(t*t+1),s=t*c;for(let i=0;i<k;i++){const a=A[i][p],b=A[i][q];A[i][p]=c*a-s*b;A[i][q]=s*a+c*b;}for(let i=0;i<k;i++){const a=A[p][i],b=A[q][i];A[p][i]=c*a-s*b;A[q][i]=s*a+c*b;}for(let i=0;i<k;i++){const a=V[i][p],b=V[i][q];V[i][p]=c*a-s*b;V[i][q]=s*a+c*b;}}}return {val:A.map((r,i)=>A[i][i]),vec:V};}
    function genEig(Kr,Mr,k){const L=Array.from({length:k},()=>new Float64Array(k));for(let i=0;i<k;i++)for(let j=0;j<=i;j++){let s=Mr[i][j];for(let m=0;m<j;m++)s-=L[i][m]*L[j][m];if(i===j)L[i][j]=Math.sqrt(Math.max(s,1e-12));else L[i][j]=s/L[j][j];}const Li=Array.from({length:k},()=>new Float64Array(k));for(let i=0;i<k;i++){Li[i][i]=1/L[i][i];for(let j=0;j<i;j++){let s=0;for(let m=j;m<i;m++)s+=L[i][m]*Li[m][j];Li[i][j]=-s/L[i][i];}}const A=Array.from({length:k},()=>new Float64Array(k));for(let i=0;i<k;i++)for(let j=0;j<k;j++){let s=0;for(let a=0;a<k;a++)for(let b=0;b<k;b++)s+=Li[i][a]*Kr[a][b]*Li[j][b];A[i][j]=s;}const {val,vec}=jac(A.map(r=>Array.from(r)),k);const C=Array.from({length:k},()=>new Float64Array(k));for(let col=0;col<k;col++)for(let i=0;i<k;i++){let s=0;for(let a=0;a<k;a++)s+=Li[a][i]*vec[a][col];C[i][col]=s;}return {val,C};}
    function blockEig(de,S,dg,Phi0){const q=kM+guard;let Phi=Phi0;if(!Phi){Phi=[];let sd=3;const rnd=()=>{sd=(sd*1103515245+12345)&0x7fffffff;return sd/0x7fffffff-0.5;};for(let c=0;c<q;c++){const v=new Float64Array(nD);for(let d=0;d<nD;d++)v[d]=fixed[d]?0:rnd();Phi.push(v);}}let lams=new Array(kM).fill(0),x0s=Phi.map(()=>null);const tmp=new Float64Array(nD);
      for(let it=0;it<40;it++){const Psi=[];for(let c=0;c<q;c++){mvKg(de,S,Phi[c],tmp);for(let d=0;d<nD;d++)tmp[d]=-tmp[d];const ps=pcg(de,dg,tmp,1e-9,4000,x0s[c]);x0s[c]=ps;Psi.push(ps);}
        const Kr=Array.from({length:q},()=>new Float64Array(q)),Mr=Array.from({length:q},()=>new Float64Array(q)),KP=[],MP=[];
        for(let c=0;c<q;c++){const a=new Float64Array(nD);mvK(de,Psi[c],a);KP.push(a);const b=new Float64Array(nD);mvKg(de,S,Psi[c],b);for(let d=0;d<nD;d++)b[d]=-b[d];MP.push(b);}
        for(let i=0;i<q;i++)for(let j=0;j<q;j++){let sk=0,sm=0;for(let d=0;d<nD;d++){sk+=Psi[i][d]*KP[j][d];sm+=Psi[i][d]*MP[j][d];}Kr[i][j]=sk;Mr[i][j]=sm;}
        const {val,C}=genEig(Kr,Mr,q),ord=val.map((v,i)=>[v,i]).sort((a,b)=>a[0]-b[0]).map(x=>x[1]);
        const nP=[],nL=[];for(let c=0;c<q;c++){const sc=ord[c],v=new Float64Array(nD);for(let j=0;j<q;j++){const cj=C[j][sc];for(let d=0;d<nD;d++)v[d]+=cj*Psi[j][d];}let n=0;for(let d=0;d<nD;d++)n+=v[d]*v[d];n=Math.sqrt(n)||1;for(let d=0;d<nD;d++)v[d]/=n;nP.push(v);nL.push(val[sc]);}
        Phi=nP;let conv=true;for(let c=0;c<kM;c++)if(Math.abs(nL[c]-lams[c])>1e-6*Math.abs(nL[c]))conv=false;lams=nL.slice(0,kM);if(conv&&it>3)break;}
      return {lams,modes:Phi.slice(0,kM),full:Phi};}
    function modeGrad(de,S,u,dg,lam,phi){const Kg=new Float64Array(nD);mvKg(de,S,phi,Kg);let Den=0;for(let d=0;d<nD;d++)Den+=-phi[d]*Kg[d];
      const Psi=new Float64Array(nD),pe=new Float64Array(24),m6a=new Array(nel);
      for(let e=0;e<nel;e++){const el=elems[e];for(let a=0;a<8;a++){pe[3*a]=phi[3*el[a]];pe[3*a+1]=phi[3*el[a]+1];pe[3*a+2]=phi[3*el[a]+2];}const m6=new Float64Array(6);
        for(const gp of gpg){const g=gp.g,dJ=gp.detJ,G=[[0,0,0],[0,0,0],[0,0,0]];for(let a=0;a<8;a++){const ga=g[a];for(let c=0;c<3;c++){const pc=pe[3*a+c];G[c][0]+=pc*ga[0];G[c][1]+=pc*ga[1];G[c][2]+=pc*ga[2];}}const M=[[0,0,0],[0,0,0],[0,0,0]];for(let m=0;m<3;m++)for(let n=0;n<3;n++){let s=0;for(let c=0;c<3;c++)s+=G[c][m]*G[c][n];M[m][n]=s;}m6[0]+=M[0][0]*dJ;m6[1]+=M[1][1]*dJ;m6[2]+=M[2][2]*dJ;m6[3]+=2*M[0][1]*dJ;m6[4]+=2*M[1][2]*dJ;m6[5]+=2*M[2][0]*dJ;}
        m6a[e]=m6;const Dm=new Float64Array(6);for(let r=0;r<6;r++){let s=0;for(let c=0;c<6;c++)s+=D[r][c]*m6[c];Dm[r]=s;}const Bt=new Float64Array(24);for(let a=0;a<24;a++){let s=0;for(let r=0;r<6;r++)s+=B0[r][a]*Dm[r];Bt[a]=s;}const r3=de[e];for(let a=0;a<8;a++){const n=el[a];Psi[3*n]+=r3*Bt[3*a];Psi[3*n+1]+=r3*Bt[3*a+1];Psi[3*n+2]+=r3*Bt[3*a+2];}}
      for(let d=0;d<nD;d++)if(fixed[d])Psi[d]=0;const rg=new Float64Array(nD);for(let d=0;d<nD;d++)rg[d]=lam*Psi[d];const ga=pcg(de,dg,rg,1e-9,4000);
      const grad=new Float64Array(nel),uu=new Float64Array(24),ge=new Float64Array(24);
      for(let e=0;e<nel;e++){const el=elems[e],re=Math.cbrt(de[e]),r2=3*re*re;for(let a=0;a<8;a++){pe[3*a]=phi[3*el[a]];pe[3*a+1]=phi[3*el[a]+1];pe[3*a+2]=phi[3*el[a]+2];uu[3*a]=u[3*el[a]];uu[3*a+1]=u[3*el[a]+1];uu[3*a+2]=u[3*el[a]+2];ge[3*a]=ga[3*el[a]];ge[3*a+1]=ga[3*el[a]+1];ge[3*a+2]=ga[3*el[a]+2];}
        let pKp=0;for(let a=0;a<24;a++){let s=0;const Ka=Ke[a];for(let b=0;b<24;b++)s+=Ka[b]*pe[b];pKp+=pe[a]*s;}let sm=0;for(let c=0;c<6;c++)sm+=S[e][c]*m6a[e][c];let gKu=0;for(let a=0;a<24;a++){let s=0;const Ka=Ke[a];for(let b=0;b<24;b++)s+=Ka[b]*uu[b];gKu+=ge[a]*s;}
        grad[e]=(r2*pKp+lam*r2*sm-r2*gKu)/Den;}return grad;}
    const st=[],R=Math.floor(rmin);for(let dk=-R;dk<=R;dk++)for(let dj=-R;dj<=R;dj++)for(let di=-R;di<=R;di++){const dd=Math.hypot(di,dj,dk),w=rmin-dd;if(w>0)st.push([di,dj,dk,w]);}
    function filt(rho,x){const o=new Float64Array(nel);for(let k=0;k<nz;k++)for(let j=0;j<ny;j++)for(let i=0;i<nx;i++){let s=0,sw=0;for(const q of st){const ii=i+q[0],jj=j+q[1],kk=k+q[2];if(ii<0||jj<0||kk<0||ii>=nx||jj>=ny||kk>=nz)continue;const rr=Math.max(rho[eidx(ii,jj,kk)],1e-3);s+=q[3]*rr*x[eidx(ii,jj,kk)];sw+=q[3]*rr;}o[eidx(i,j,k)]=s/(sw*Math.max(rho[eidx(i,j,k)],1e-3));}return o;}
    let rho=new Float64Array(nel).fill(vf); for(let e=0;e<nel;e++) if(prot[e]) rho[e]=1;
    let Phi0=null, blf0=0, blf=0; const hist=[];
    for(let it=0; it<iters; it++){
      const de=Array.from(rho,r=>r*r*r), dg=diagOf(de), u=pcg(de,dg,f,1e-10,4000), S=sig0(u);
      const {lams,modes,full}=blockEig(de,S,dg,Phi0); Phi0=full;
      blf=lams[0]; if(it===0) blf0=blf; hist.push(blf);
      if(opts.onIter) opts.onIter(it, blf);
      const l1=lams[0], ex=lams.map(l=>Math.exp(-ksP*(l-l1))), se=ex.reduce((a,b)=>a+b,0), w=ex.map(e=>e/se);
      const dks=new Float64Array(nel); for(let c=0;c<kM;c++){ const gc=modeGrad(de,S,u,dg,lams[c],modes[c]); for(let e=0;e<nel;e++) dks[e]+=w[c]*gc[e]; }
      const dgf=filt(rho,dks);
      let a=0,b=1e9,rn=rho; for(let bs=0;bs<80&&(b-a)>1e-6*(a+b);bs++){ const lm=0.5*(a+b); rn=new Float64Array(nel); let vol=0;
        for(let e=0;e<nel;e++){ if(prot[e]){rn[e]=1;vol+=1;continue;} let v=rho[e]*Math.sqrt(Math.max(dgf[e],0)/lm); v=Math.max(1e-3,Math.max(rho[e]-move,Math.min(1,Math.min(rho[e]+move,v)))); rn[e]=v; vol+=v; }
        if(vol>vf*nel) a=lm; else b=lm; }
      rho=rn;
    }
    // Endzustand + Mode frisch (verlässlich)
    const de=Array.from(rho,r=>r*r*r), dg=diagOf(de), u=pcg(de,dg,f,1e-11,4000), S=sig0(u);
    const fin=blockEig(de,S,dg,null); blf=fin.lams[0];
    return { rho:Float32Array.from(rho), blf0, blf, hist, mode:Float32Array.from(fin.modes[0]), iter:iters };
  }

  global.BUCK={ analyze, optimize };
})(typeof window!=='undefined'?window:globalThis);
