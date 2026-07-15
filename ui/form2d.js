// form2d.js — 2D-Formfindung: Plane-Stress QUAD4 FEM (matrixfrei, CG) + SIMP + grobe SKO.
// Selbst-enthaltend: headless (node) und im Browser (window.Form2D).
// Quadratische Elemente (Einheitskante). Knoten (i,j): idx=i*(nely+1)+j; DOFs 2*idx (x), 2*idx+1 (y).
(function(global){
'use strict';

// 8x8 Element-Steifigkeit, Einheits-Quadrat, E=1, plane stress (top88-Konvention).
function elemKE(nu){
  const k=[ 1/2-nu/6, 1/8+nu/8, -1/4-nu/12, -1/8+3*nu/8,
           -1/4+nu/12, -1/8-nu/8, nu/6, 1/8-3*nu/8 ];
  const P=[[0,1,2,3,4,5,6,7],[1,0,7,6,5,4,3,2],[2,7,0,5,6,3,4,1],[3,6,5,0,7,2,1,4],
           [4,5,6,7,0,1,2,3],[5,4,3,2,1,0,7,6],[6,3,4,1,2,7,0,5],[7,2,1,4,3,6,5,0]];
  const f=1/(1-nu*nu), KE=[];
  for(let i=0;i<8;i++){ const r=new Float64Array(8); for(let j=0;j<8;j++) r[j]=f*k[P[i][j]]; KE.push(r); }
  return KE;
}
function edofOf(ex,ey,nely){ const n1=ex*(nely+1)+ey, n2=n1+(nely+1);
  return [2*n1,2*n1+1, 2*n2,2*n2+1, 2*n2+2,2*n2+3, 2*n1+2,2*n1+3]; }

function makeCtx(o){ const nelx=o.nelx, nely=o.nely, nu=o.nu!=null?o.nu:0.3, ne=nelx*nely;
  const KE=elemKE(nu), edof=new Array(ne), Ee=new Float64Array(ne);
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++) edof[ex*nely+ey]=edofOf(ex,ey,nely);
  const nd=2*(nelx+1)*(nely+1), fixed=new Uint8Array(nd); if(o.fixed) fixed.set(o.fixed);
  return {nelx,nely,ne,KE,edof,Ee,fixed,nd,E0:o.E0!=null?o.E0:1,Emin:o.Emin!=null?o.Emin:1e-9,p:o.p!=null?o.p:3};
}
function setStiffness(ctx,dens){ const {Ee,E0,Emin,p,ne}=ctx;
  for(let e=0;e<ne;e++){ let x=dens?dens[e]:1; x=x<0?0:x>1?1:x; Ee[e]=Emin+Math.pow(x,p)*(E0-Emin); } }

function applyK(u,out,ctx){ out.fill(0); const {ne,KE,Ee,edof,fixed}=ctx;
  for(let e=0;e<ne;e++){ const ed=edof[e], se=Ee[e];
    for(let a=0;a<8;a++){ const Ka=KE[a]; let s=0; for(let b=0;b<8;b++) s+=Ka[b]*u[ed[b]]; out[ed[a]]+=se*s; } }
  for(let d=0,nd=out.length;d<nd;d++) if(fixed[d]) out[d]=u[d];   // fixierte DOFs = Identitaet
}
function cg(ctx,f,u0,maxIter,tol){ const nd=ctx.nd, u=new Float64Array(nd); if(u0) u.set(u0);
  const diag=new Float64Array(nd);
  { const {ne,KE,Ee,edof}=ctx; for(let e=0;e<ne;e++){ const ed=edof[e], se=Ee[e]; for(let a=0;a<8;a++) diag[ed[a]]+=se*KE[a][a]; } }
  for(let d=0;d<nd;d++){ if(ctx.fixed[d]||!(diag[d]>0)) diag[d]=1; }
  const Ku=new Float64Array(nd); applyK(u,Ku,ctx);
  const r=new Float64Array(nd), z=new Float64Array(nd), pv=new Float64Array(nd), Ap=new Float64Array(nd);
  let bnorm=0; for(let d=0;d<nd;d++){ r[d]=ctx.fixed[d]?0:f[d]-Ku[d]; z[d]=r[d]/diag[d]; pv[d]=z[d]; if(!ctx.fixed[d]) bnorm+=f[d]*f[d]; }
  bnorm=Math.sqrt(bnorm)||1; let rz=0; for(let d=0;d<nd;d++) rz+=r[d]*z[d];
  let it=0; for(;it<maxIter;it++){
    applyK(pv,Ap,ctx); let pAp=0; for(let d=0;d<nd;d++) pAp+=pv[d]*Ap[d]; const al=rz/(pAp||1e-30);
    for(let d=0;d<nd;d++){ u[d]+=al*pv[d]; r[d]-=al*Ap[d]; }
    let rn=0; for(let d=0;d<nd;d++) rn+=r[d]*r[d]; if(Math.sqrt(rn)/bnorm<tol){ it++; break; }
    for(let d=0;d<nd;d++) z[d]=r[d]/diag[d]; let rz2=0; for(let d=0;d<nd;d++) rz2+=r[d]*z[d];
    const be=rz2/(rz||1e-30); rz=rz2; for(let d=0;d<nd;d++) pv[d]=z[d]+be*pv[d];
  }
  return {u,iter:it};
}
function elemCe(ctx,u){ const {ne,KE,edof}=ctx, ce=new Float64Array(ne);
  for(let e=0;e<ne;e++){ const ed=edof[e]; let s=0;
    for(let a=0;a<8;a++){ const Ka=KE[a]; let t=0; for(let b=0;b<8;b++) t+=Ka[b]*u[ed[b]]; s+=u[ed[a]]*t; } ce[e]=s; }
  return ce;   // ue^T KE0 ue  (Vollmaterial-Dehnungsenergie)
}
function solve2D(o){ const ctx=makeCtx(o); setStiffness(ctx,o.dens);
  const f=new Float64Array(ctx.nd); if(o.loads) for(const L of o.loads) f[L[0]]+=L[1];
  const {u,iter}=cg(ctx,f,o.u0,o.maxIter||2000,o.tol||1e-6);
  const ce=elemCe(ctx,u); let C=0; for(let e=0;e<ctx.ne;e++) C+=ctx.Ee[e]*ce[e];
  return {u,ce,C,iter};
}

// ---- Dichtefilter (2D) ----
function buildFilter(nelx,nely,rmin){ const st=[], R=Math.ceil(rmin)-1;
  for(let di=-R;di<=R;di++)for(let dj=-R;dj<=R;dj++){ const w=rmin-Math.hypot(di,dj); if(w>0) st.push([di,dj,w]); }
  const Hs=new Float64Array(nelx*nely);
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ let s=0; for(const q of st){ const ii=ex+q[0],jj=ey+q[1]; if(ii<0||jj<0||ii>=nelx||jj>=nely)continue; s+=q[2]; } Hs[ex*nely+ey]=s||1; }
  return {st,Hs,nelx,nely};
}
function convFilter(x,flt){ const {st,Hs,nelx,nely}=flt, o=new Float64Array(nelx*nely);
  for(let ex=0;ex<nelx;ex++)for(let ey=0;ey<nely;ey++){ let s=0; for(const q of st){ const ii=ex+q[0],jj=ey+q[1]; if(ii<0||jj<0||ii>=nelx||jj>=nely)continue; s+=q[2]*x[ii*nely+jj]; } o[ex*nely+ey]=s/Hs[ex*nely+ey]; }
  return o;
}

// ---- SIMP (Dichtefilter der Sensitivitaet + OC) ----
function simp2D(o,cb){ const nelx=o.nelx,nely=o.nely,ne=nelx*nely, vf=o.volfrac!=null?o.volfrac:0.4;
  const iters=o.iters||60, rmin=Math.max(1.2,o.rmin||1.5), p=o.p!=null?o.p:3, move=0.2, E0=o.E0!=null?o.E0:1, Emin=o.Emin!=null?o.Emin:1e-9;
  const dom=o.dom, flt=buildFilter(nelx,nely,rmin); let domCount=0; for(let e=0;e<ne;e++) if(!dom||dom[e]) domCount++;
  let x=new Float64Array(ne); for(let e=0;e<ne;e++) x[e]=(dom&&!dom[e])?0:vf; let u0=null, lastC=0;
  for(let it=0;it<iters;it++){
    const res=solve2D({nelx,nely,nu:o.nu,E0,Emin,p,fixed:o.fixed,loads:o.loads,dens:x,u0,maxIter:1500,tol:1e-5}); u0=res.u; lastC=res.C;
    const dc=new Float64Array(ne); for(let e=0;e<ne;e++){ const xe=Math.max(x[e],1e-3); dc[e]=-p*Math.pow(xe,p-1)*(E0-Emin)*res.ce[e]; }
    const xdc=new Float64Array(ne); for(let e=0;e<ne;e++) xdc[e]=x[e]*dc[e];
    const dcf=convFilter(xdc,flt); for(let e=0;e<ne;e++) dc[e]=dcf[e]/Math.max(1e-3,x[e]);   // klassischer Sensitivitaetsfilter
    let l1=0,l2=1e9,xnew=x;
    for(let bs=0;bs<90 && (l2-l1)>1e-6*(l1+l2)+1e-15;bs++){ const lm=0.5*(l1+l2); xnew=new Float64Array(ne); let vol=0;
      for(let e=0;e<ne;e++){ if(dom&&!dom[e]){xnew[e]=0;continue;} const be=Math.sqrt(Math.max(-dc[e],0)/lm);
        let xe=x[e]*be; xe=Math.min(1,Math.min(x[e]+move,xe)); xe=Math.max(1e-3,Math.max(x[e]-move,xe)); xnew[e]=xe; vol+=xe; }
      if(vol>vf*domCount) l1=lm; else l2=lm; }
    x=xnew; if(cb) cb(it,lastC,x);
  }
  return {x,C:lastC};
}

// ---- Grobe SKO-Vorschau (energiebasiert, wenige Iterationen) ----
function sko2D(o,cb){ const ne=o.nelx*o.nely, dom=o.dom;   // robuste Kurz-Vorschau = wenige SIMP-Iterationen, dann binarisiert
  const r=simp2D(Object.assign({},o,{iters:o.iters||12, rmin:Math.max(1.2,o.rmin||1.4)}), cb);
  const mask=new Float64Array(ne); for(let e=0;e<ne;e++) mask[e]=((!dom||dom[e]) && r.x[e]>0.5)?1:0;
  return {mask,x:r.x,C:r.C};
}

const API={elemKE,edofOf,solve2D,simp2D,sko2D,buildFilter,convFilter};
if(typeof module!=='undefined' && module.exports) module.exports=API;
global.Form2D=API;
})(typeof window!=='undefined'?window:globalThis);
