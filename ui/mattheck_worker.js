// Web Worker: FE-Solve im Hintergrund-Thread (Float32-Solver). Erwartet typed arrays statt Callbacks.
// Wird genutzt, wenn die Seite über den Server (http) geladen ist; sonst rechnet mattheck.html im Fallback.
importScripts('mattheck_fem.js');
importScripts('mattheck_buckling.js');

onmessage = function(e){
  const d = e.data;
  if(d.cmd === 'ping'){ postMessage({type:'pong'}); return; }
  if(d.cmd === 'buckle'){
    try{
      const bx=d.bx, by=d.by, bz=d.bz; const vi=(i,j,k)=>(k*by+j)*bx+i;
      const active=(i,j,k)=> i>=0&&j>=0&&k>=0&&i<bx&&j<by&&k<bz && d.dom[vi(i,j,k)]===1;
      const isFixed=n=> d.fixed[n]===1;
      const forces=[]; for(let t=0;t<d.fNodes.length;t++) forces.push([d.fNodes[t],[d.fVec[3*t],d.fVec[3*t+1],d.fVec[3*t+2]]]);
      const opts={ nx:bx, ny:by, nz:bz, h:d.h, E:d.E, nu:d.nu, active, isFixed, forces, maxEig:d.maxEig||40, tol:d.tol||1e-7 };
      if(d.fixedDof) opts.fixedDof=d.fixedDof;
      if(d.densArr) opts.dens=(i,j,k)=>d.densArr[vi(i,j,k)];
      const res=BUCK.analyze(opts);
      postMessage({ type:'buckled', blf:res.blf, mode:res.mode, iter:res.iter }, [res.mode.buffer]);
    }catch(err){ postMessage({ type:'buckled', error:String(err && err.message || err) }); }
    return;
  }
  if(d.cmd === 'buckleopt'){
    try{
      const bx=d.bx, by=d.by, bz=d.bz; const vi=(i,j,k)=>(k*by+j)*bx+i;
      const isFixed=n=> d.fixed[n]===1;
      const forces=[]; for(let t=0;t<d.fNodes.length;t++) forces.push([d.fNodes[t],[d.fVec[3*t],d.fVec[3*t+1],d.fVec[3*t+2]]]);
      const protect = d.protArr ? (i,j,k)=>d.protArr[vi(i,j,k)]===1 : undefined;
      const res=BUCK.optimize({ nx:bx, ny:by, nz:bz, h:d.h, E:d.E, nu:d.nu, isFixed, fixedDof:d.fixedDof, forces, protect,
        volfrac:d.volfrac, iters:d.iters, move:d.move, rmin:d.rmin, kModes:d.kModes, guard:d.guard, ksP:d.ksP,
        onIter:(it,blf)=>postMessage({type:'buckleopt-progress', it, blf}) });
      postMessage({ type:'buckleopt', rho:res.rho, blf0:res.blf0, blf:res.blf, mode:res.mode, iter:res.iter }, [res.rho.buffer, res.mode.buffer]);
    }catch(err){ postMessage({ type:'buckleopt', error:String(err && err.message || err) }); }
    return;
  }
  if(d.cmd !== 'solve') return;
  try{
    const bx=d.bx, by=d.by, bz=d.bz, Ntot=bx*by*bz;
    const vi=(i,j,k)=>(k*by+j)*bx+i;
    const active=(i,j,k)=> i>=0&&j>=0&&k>=0&&i<bx&&j<by&&k<bz && d.dom[vi(i,j,k)]===1;
    const isFixed=n=> d.fixed[n]===1;
    const forces=[]; for(let t=0;t<d.fNodes.length;t++) forces.push([d.fNodes[t],[d.fVec[3*t],d.fVec[3*t+1],d.fVec[3*t+2]]]);
    const opts={ nx:bx, ny:by, nz:bz, h:d.h, E:d.E, nu:d.nu, active, isFixed, forces, maxIter:d.maxIter||2000, tol:d.tol||1e-4 };
    if(d.fixedDof) opts.fixedDof=d.fixedDof;
    if(d.densArr) opts.dens=(i,j,k)=>{ const r=d.densArr[vi(i,j,k)]; return r*r*r; };   // SIMP: rho^3
    if(d.u0) opts.u0=d.u0;
    const res=FEM.solve(opts);
    const vm=new Float32Array(Ntot), se=new Float32Array(Ntot);
    for(const ee of res.vm){ const v=vi(ee.i,ee.j,ee.k); vm[v]=ee.vm; se[v]=ee.se; }
    postMessage({ type:'solved', u:res.u, vm, se, iter:res.iter, resid:res.resid }, [res.u.buffer, vm.buffer, se.buffer]);
  }catch(err){ postMessage({ type:'solved', error:String(err && err.message || err) }); }
};
