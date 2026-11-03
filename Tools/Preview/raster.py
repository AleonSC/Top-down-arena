import numpy as np
def raster(v, W, H, ss=2, bg=(10,12,18)):
    """GL triangles (x, y, r, g, b, a per vertex; y up) drawn like the GPU: colour blended across each triangle,
    alpha blending, additive where alpha < 0 (-1 - a), supersampled ss x ss for smooth edges."""
    Ws, Hs = W*ss, H*ss
    buf = np.empty((Hs, Ws, 3), np.float32); buf[:] = np.array(bg, np.float32)/255.0
    a = np.asarray(v, np.float32).reshape(-1, 3, 6)
    for t in a:
        xs = t[:,0]*ss; ys = (H - t[:,1])*ss
        x0 = max(int(np.floor(xs.min())), 0); x1 = min(int(np.ceil(xs.max())), Ws-1)
        y0 = max(int(np.floor(ys.min())), 0); y1 = min(int(np.ceil(ys.max())), Hs-1)
        if x1 < x0 or y1 < y0: continue
        den = (ys[1]-ys[2])*(xs[0]-xs[2]) + (xs[2]-xs[1])*(ys[0]-ys[2])
        if abs(den) < 1e-9: continue
        px, py = np.meshgrid(np.arange(x0, x1+1)+0.5, np.arange(y0, y1+1)+0.5)
        l0 = ((ys[1]-ys[2])*(px-xs[2]) + (xs[2]-xs[1])*(py-ys[2]))/den
        l1 = ((ys[2]-ys[0])*(px-xs[2]) + (xs[0]-xs[2])*(py-ys[2]))/den
        l2 = 1 - l0 - l1
        m = (l0 >= -1e-6) & (l1 >= -1e-6) & (l2 >= -1e-6)
        if not m.any(): continue
        add = t[0,5] < 0
        al = np.where(t[:,5] < 0, -1 - t[:,5], t[:,5])
        col = t[:,2:5]
        L = np.stack([l0, l1, l2], -1)[m]
        rgb = L @ col; A = (L @ al)[:, None]
        rgb = np.clip(rgb, 0, 1); A = np.clip(A, 0, 1)
        reg = buf[y0:y1+1, x0:x1+1]
        if add: reg[m] = reg[m] + rgb*A
        else: reg[m] = reg[m]*(1-A) + rgb*A
    out = np.clip(buf, 0, 1)
    out = out.reshape(H, ss, W, ss, 3).mean((1, 3))
    return (out*255).astype(np.uint8)
