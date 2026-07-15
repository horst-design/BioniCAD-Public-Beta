using System.Numerics;
using PicoGK;

namespace LatticeFraktal.Pipeline;

/// <summary>
/// Feldgesteuertes IMPLIZITES Gitter (Gyroid-TPMS oder Streben-Zelle) mit
/// CAIO-Verzerrung (Koordinaten pro Punkt am Hauptspannungs-Frame ausgerichtet),
/// Dichte-Grading (Wandstärke/Radius aus dem von-Mises-Feld) und Clip an die
/// SKO/CAO-Form (occ). Da alles implizit ist, ist das Ergebnis konnektiv und
/// wasserdicht per Konstruktion — keine losen Streben.
///
/// KRAFTFLUSS: Rohe FEM-Eigenvektoren pro Voxel sind inkohärent (Vorzeichen/
/// Reihenfolge springen zwischen Nachbarn), deshalb wird aus dirs+pvals der
/// symmetrische Spannungstensor rekonstruiert, RÄUMLICH GEGLÄTTET (Box-Blur,
/// Iterationen ∝ flowSmooth) und neu zerlegt → ein kohärentes, nach |σ|
/// sortiertes Frame (Achse 0 = stärkste Hauptspannung = Flussrichtung).
///   • Intensität (caio 0..1): Lerp Weltkoordinate ↔ Frame-Koordinate.
///   • Länge (flowLen -1..1): anisotrope Streckung entlang der Flussachse
///     (>0 längliche Zellen entlang Fluss, <0 gestaucht).
///   • Smoothing (flowSmooth 0..1): Kohärenz/Glättung des Richtungsfelds.
///
/// Vertrag (aus mattheck.html „CAIO-Feld-JSON"):
///   { bx,by,bz,h, occ:[Ntot 0/1], field:[Ntot 0..1 vM], dirs:[Ntot*9], pvals:[Ntot*3] }
/// </summary>
public sealed class FieldLatticeImplicit : IImplicit
{
    readonly int bx, by, bz; readonly float h;
    readonly float[] occf, field, dirsS;   // dirsS = geglättetes, sortiertes Frame (Ntot*9)
    readonly float cell, isoLo, isoHi, caio, flowLen; readonly string type;
    readonly bool clipShape;               // occ-Clip im Impliziten? (false = scharfer Mesh-Clip via BoolIntersect)
    public readonly BBox3 Bounds;

    public FieldLatticeImplicit(int bx, int by, int bz, float h,
                                int[] occ, float[] field, float[] dirs, float[]? pvals,
                                float cell, float vol, float grading, float caio,
                                float flowLen, float flowSmooth, string type, bool clipShape=true)
    {
        this.bx=bx; this.by=by; this.bz=bz; this.h=h; this.field=field;
        this.cell=MathF.Max(cell, 0.5f);
        this.caio=Math.Clamp(caio, 0f, 1f);
        this.flowLen=Math.Clamp(flowLen, -1f, 1f);
        this.clipShape=clipShape;
        this.type=(type ?? "gyroid").ToLowerInvariant();
        occf=new float[occ.Length]; for(int i=0;i<occ.Length;i++) occf[i]=occ[i];
        // Volumenanteil -> Basis-iso; Grading spreizt iso mit der Spannung (dick wo Last).
        float baseIso = 0.25f + 1.1f*Math.Clamp(vol, 0.05f, 0.95f);
        float g = Math.Clamp(grading, 0f, 1f);
        isoLo = baseIso*(1f-0.6f*g); isoHi = baseIso*(1f+0.6f*g);
        Bounds = new BBox3(new Vector3(0,0,0), new Vector3(bx*h, by*h, bz*h));

        dirsS = BuildCoherentFrame(bx,by,bz, dirs, pvals, occf, Math.Clamp(flowSmooth,0f,1f));
    }

    int Ci(int v,int hi)=> v<0?0:(v>hi?hi:v);
    int Idx(int i,int j,int k)=>(k*by+j)*bx+i;

    // -------- Kohärentes Frame: Tensor bauen -> glätten -> neu zerlegen -> nach |λ| sortieren --------
    static float[] BuildCoherentFrame(int bx,int by,int bz, float[] dirs, float[]? pvals, float[] occ, float smooth)
    {
        int N=bx*by*bz;
        // Symmetrischer Tensor pro Voxel: T = Σ_a λ_a (v_a ⊗ v_a). 6 Komponenten [xx,yy,zz,xy,xz,yz].
        var T=new float[N*6]; var wgt=new float[N];
        for(int v=0; v<N; v++)
        {
            if(dirs.Length < v*9+9) break;
            float w=0;
            for(int a=0; a<3; a++)
            {
                float lx=dirs[v*9+3*a], ly=dirs[v*9+3*a+1], lz=dirs[v*9+3*a+2];
                float lam = (pvals!=null && pvals.Length>=v*3+3) ? pvals[v*3+a] : 1f;
                T[v*6+0]+=lam*lx*lx; T[v*6+1]+=lam*ly*ly; T[v*6+2]+=lam*lz*lz;
                T[v*6+3]+=lam*lx*ly; T[v*6+4]+=lam*lx*lz; T[v*6+5]+=lam*ly*lz;
                w+=MathF.Abs(lam);
            }
            // Maske: nur Voxel mit Form UND vorhandenem Frame gehen in die Glättung ein.
            wgt[v] = (occ[v]>0.5f && w>1e-9f) ? 1f : 0f;
        }
        // Box-Blur (3×3×3, maskiert) — Iterationen aus smooth. Erhält Symmetrie/Kohärenz.
        int passes = (int)MathF.Round(smooth*10f);
        if(passes>0)
        {
            var Tb=new float[N*6]; var wb=new float[N];
            int IdxL(int i,int j,int k)=>(k*by+j)*bx+i;
            for(int it=0; it<passes; it++)
            {
                System.Array.Clear(Tb,0,Tb.Length); System.Array.Clear(wb,0,wb.Length);
                for(int k=0;k<bz;k++)for(int j=0;j<by;j++)for(int i=0;i<bx;i++)
                {
                    int v=IdxL(i,j,k);
                    float acc0=0,acc1=0,acc2=0,acc3=0,acc4=0,acc5=0,accW=0;
                    for(int dk=-1;dk<=1;dk++)for(int dj=-1;dj<=1;dj++)for(int di=-1;di<=1;di++)
                    {
                        int ii=i+di, jj=j+dj, kk=k+dk;
                        if(ii<0||jj<0||kk<0||ii>=bx||jj>=by||kk>=bz) continue;
                        int u=IdxL(ii,jj,kk); float wu=wgt[u]; if(wu<=0f) continue;
                        acc0+=T[u*6+0]*wu; acc1+=T[u*6+1]*wu; acc2+=T[u*6+2]*wu;
                        acc3+=T[u*6+3]*wu; acc4+=T[u*6+4]*wu; acc5+=T[u*6+5]*wu; accW+=wu;
                    }
                    if(accW>0f)
                    {
                        float inv=1f/accW;
                        Tb[v*6+0]=acc0*inv; Tb[v*6+1]=acc1*inv; Tb[v*6+2]=acc2*inv;
                        Tb[v*6+3]=acc3*inv; Tb[v*6+4]=acc4*inv; Tb[v*6+5]=acc5*inv;
                        wb[v]=wgt[v]>0f?1f:0f;
                    }
                }
                System.Array.Copy(Tb,T,Tb.Length); System.Array.Copy(wb,wgt,wb.Length);
            }
        }
        // Neu zerlegen -> sortiertes Frame (Achse0 = größte |Hauptspannung| = Fluss). Fallback Identität.
        var outDirs=new float[N*9];
        for(int v=0; v<N; v++)
        {
            if(wgt[v]<=0f){ outDirs[v*9]=1; outDirs[v*9+4]=1; outDirs[v*9+8]=1; continue; }
            Eig3(T[v*6+0],T[v*6+1],T[v*6+2],T[v*6+3],T[v*6+4],T[v*6+5], out var val, out var vec);
            // nach |λ| absteigend sortieren
            int[] ord={0,1,2};
            for(int a=0;a<2;a++)for(int b=a+1;b<3;b++)
                if(MathF.Abs(val[ord[b]])>MathF.Abs(val[ord[a]])){ (ord[a],ord[b])=(ord[b],ord[a]); }
            for(int a=0;a<3;a++){ var d=vec[ord[a]];
                outDirs[v*9+3*a]=d.X; outDirs[v*9+3*a+1]=d.Y; outDirs[v*9+3*a+2]=d.Z; }
        }
        return outDirs;
    }

    // Symmetrische 3×3-Eigenzerlegung (zyklisches Jacobi). Eingabe m=[xx,yy,zz,xy,xz,yz].
    static void Eig3(float mxx,float myy,float mzz,float mxy,float mxz,float myz,
                     out float[] val, out Vector3[] vec)
    {
        double[,] a={{mxx,mxy,mxz},{mxy,myy,myz},{mxz,myz,mzz}};
        double[,] v={{1,0,0},{0,1,0},{0,0,1}};
        for(int it=0; it<50; it++)
        {
            double off=Math.Abs(a[0,1])+Math.Abs(a[0,2])+Math.Abs(a[1,2]);
            if(off<1e-16) break;
            for(int p=0;p<2;p++)for(int q=p+1;q<3;q++)
            {
                if(Math.Abs(a[p,q])<1e-22) continue;
                double th=(a[q,q]-a[p,p])/(2*a[p,q]);
                double t=(th==0)?1:(Math.Sign(th)/(Math.Abs(th)+Math.Sqrt(th*th+1)));
                double c=1/Math.Sqrt(t*t+1), s=t*c, apq=a[p,q];
                a[p,p]-=t*apq; a[q,q]+=t*apq; a[p,q]=a[q,p]=0;
                for(int k=0;k<3;k++) if(k!=p&&k!=q){ double akp=a[k,p], akq=a[k,q];
                    a[k,p]=a[p,k]=c*akp-s*akq; a[k,q]=a[q,k]=s*akp+c*akq; }
                for(int k=0;k<3;k++){ double vkp=v[k,p], vkq=v[k,q];
                    v[k,p]=c*vkp-s*vkq; v[k,q]=s*vkp+c*vkq; }
            }
        }
        val=new float[]{ (float)a[0,0], (float)a[1,1], (float)a[2,2] };
        vec=new Vector3[3];
        for(int col=0; col<3; col++)
        {
            var e=new Vector3((float)v[0,col],(float)v[1,col],(float)v[2,col]);
            float len=e.Length(); vec[col]= len>1e-9f ? e/len : Vector3.UnitX;
        }
    }

    float Trilinear(float[] a, in Vector3 p)
    {
        float fx=p.X/h-0.5f, fy=p.Y/h-0.5f, fz=p.Z/h-0.5f;
        int i0=(int)MathF.Floor(fx), j0=(int)MathF.Floor(fy), k0=(int)MathF.Floor(fz);
        float tx=fx-i0, ty=fy-j0, tz=fz-k0;
        float S(int i,int j,int k){ i=Ci(i,bx-1); j=Ci(j,by-1); k=Ci(k,bz-1); return a[Idx(i,j,k)]; }
        float c00=S(i0,j0,k0)*(1-tx)+S(i0+1,j0,k0)*tx;
        float c10=S(i0,j0+1,k0)*(1-tx)+S(i0+1,j0+1,k0)*tx;
        float c01=S(i0,j0,k0+1)*(1-tx)+S(i0+1,j0,k0+1)*tx;
        float c11=S(i0,j0+1,k0+1)*(1-tx)+S(i0+1,j0+1,k0+1)*tx;
        return (c00*(1-ty)+c10*ty)*(1-tz) + (c01*(1-ty)+c11*ty)*tz;
    }

    void FrameAt(in Vector3 p, out Vector3 v1, out Vector3 v2, out Vector3 v3)
    {
        int i=Ci((int)(p.X/h),bx-1), j=Ci((int)(p.Y/h),by-1), k=Ci((int)(p.Z/h),bz-1);
        int b=Idx(i,j,k)*9;
        v1=new Vector3(dirsS[b],dirsS[b+1],dirsS[b+2]);
        v2=new Vector3(dirsS[b+3],dirsS[b+4],dirsS[b+5]);
        v3=new Vector3(dirsS[b+6],dirsS[b+7],dirsS[b+8]);
    }

    public float fSignedDistance(in Vector3 p)
    {
        // Form-SDF aus occ (0..1): <0 innen. Trilinear -> glatte (marching-cubes-artige) Hülle.
        float occ = Trilinear(occf, p);
        float shapeSD = (0.5f - occ) * h;
        if(type=="shape") return shapeSD;   // nur die geglättete SKO/CAO-Form

        // CAIO: Koordinate am (geglätteten) Hauptspannungs-Frame ausrichten.
        FrameAt(p, out var v1, out var v2, out var v3);
        // Frame-Koordinate; Achse 0 = Flussrichtung. Länge streckt die Zelle entlang Fluss.
        float sFlow = 1f/(1f + 0.6f*flowLen);   // flowLen>0 -> größere Wellenlänge entlang Fluss (länglich)
        Vector3 wf = new Vector3(Vector3.Dot(p,v1)*sFlow, Vector3.Dot(p,v2), Vector3.Dot(p,v3));
        Vector3 w = Vector3.Lerp(p, wf, caio);
        float rho = Math.Clamp(Trilinear(field, p), 0f, 1f);

        float latSD;
        if(type=="strut")
        {
            float c=cell;
            Vector3 cc = new Vector3((MathF.Floor(w.X/c)+0.5f)*c, (MathF.Floor(w.Y/c)+0.5f)*c, (MathF.Floor(w.Z/c)+0.5f)*c);
            Vector3 d = w - cc;
            float dX=MathF.Sqrt(d.Y*d.Y+d.Z*d.Z), dY=MathF.Sqrt(d.X*d.X+d.Z*d.Z), dZ=MathF.Sqrt(d.X*d.X+d.Y*d.Y);
            float dist=MathF.Min(dX, MathF.Min(dY, dZ));
            float rad=(0.08f + 0.34f*rho)*c;   // Strebenradius aus Spannung
            latSD = dist - rad;
        }
        else   // gyroid (Sheet-TPMS): |g| < iso  -> selbsttragende Wand
        {
            float k=2f*MathF.PI/cell;
            float gy=MathF.Sin(k*w.X)*MathF.Cos(k*w.Y)+MathF.Sin(k*w.Y)*MathF.Cos(k*w.Z)+MathF.Sin(k*w.Z)*MathF.Cos(k*w.X);
            float iso=isoLo+(isoHi-isoLo)*rho;
            latSD=(MathF.Abs(gy)-iso)*(cell/(2f*MathF.PI));
        }
        // Grober occ-Clip nur ohne scharfen Mesh-Clip; sonst pures Gitter (Render schneidet per BoolIntersect).
        return clipShape ? MathF.Max(latSD, shapeSD) : latSD;
    }

    /// <summary>
    /// Rendert das implizite Feldgitter in Voxel und speichert als STL. Ist clipStlPath gesetzt,
    /// wird der Bauraum (Mesh) fein voxelisiert und das Gitter MESSERSCHARF daran geschnitten
    /// (BoolIntersect) — unabhängig von der groben FEM-Auflösung.
    /// </summary>
    public static void Render(FieldLatticeImplicit impl, float voxelSize, float fillet, string stlPfad, string? clipStlPath=null)
    {
        Exception? fehler=null;
        string log=System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lattice-fraktal-picogk-{Environment.ProcessId}.log");
        Library.Go(MathF.Max(voxelSize,0.1f), () =>
        {
            try
            {
                Voxels? clip=null;
                if(clipStlPath!=null && System.IO.File.Exists(clipStlPath))
                {
                    var msh=Mesh.mshFromStlFile(clipStlPath);
                    clip=new Voxels(msh);   // Bauraum-Mesh auf feiner Kern-Voxelgröße voxelisiert
                }
                Voxels vox;
                if(impl.type=="shape" && clip!=null)
                {
                    vox=clip;               // „Nur Form" scharf = der voxelisierte Bauraum selbst
                }
                else
                {
                    vox=new Voxels(impl, impl.Bounds);
                    if(clip!=null) vox.BoolIntersect(clip);   // Gitter ∩ Bauraum, scharf an der Mesh-Grenze
                }
                if(fillet>0f) vox.DoubleOffset(fillet, -fillet);   // leichte Glättung
                vox.mshAsMesh().SaveToStlFile(stlPfad);
            }
            catch(Exception e){ fehler=e; }
        }, strLogFilePath:log, bEndAppWithTask:true);
        if(fehler!=null) throw new Exception($"Feld-Lattice-Rendering fehlgeschlagen: {fehler.Message}", fehler);
    }
}
