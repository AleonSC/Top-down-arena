import struct, sys, os
import numpy as np
from PIL import Image, ImageDraw, ImageFont
base=sys.argv[1]; out=sys.argv[2]
W,H=1280,720
img=Image.new('RGB',(W,H),(10,12,18))
d=ImageDraw.Draw(img,'RGBA')
data=open(base+'.tris','rb').read()
v=struct.unpack('<%df'%(len(data)//4),data)
def c8(r,g,b,a): return tuple(int(max(0,min(1,x))*255) for x in (r,g,b,a))
import os
if os.environ.get('FAST') == '1':  # the old quick look: each triangle flat in its first vertex's colour
    for i in range(0,len(v),18):
        pts=[(v[i+k*6], H-v[i+k*6+1]) for k in range(3)]
        d.polygon(pts, fill=c8(*v[i+2:i+6]))
else:  # like the GPU: colour blended across each triangle, alpha blending, the glow pass adding light, smooth edges
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    from raster import raster
    img=Image.fromarray(raster(v, W, H, int(os.environ.get('SS', '2')), linear=os.environ.get('LINEAR') == '1'))  # LINEAR=1: as a Linear-colour-space Unity project shows it
    d=ImageDraw.Draw(img,'RGBA')
img=img.convert('RGBA')
fonts={}
def font(size,bold):
    k=(size,bold)
    if k not in fonts:
        fonts[k]=ImageFont.truetype('/usr/share/fonts/truetype/liberation/LiberationSans-%s.ttf'%('Bold' if bold else 'Regular'), max(6,size))
    return fonts[k]
def floats(s): return [float(x) for x in s.split(',')]
for line in open(base+'.ui',encoding='utf-8'):
    p=line.rstrip('\n').split('\t')
    kind=p[0]; x,y,w,h=floats(p[1]); cx,cy,cw,ch=floats(p[2])
    layer=Image.new('RGBA',(W,H),(0,0,0,0)); ld=ImageDraw.Draw(layer,'RGBA')
    if kind=='fill' and (w<1 or h<1): continue
    if kind=='fill':
        ld.rectangle([x,y,x+w-1,y+h-1],fill=c8(*floats(p[3])))
    elif kind=='box9':
        tint=floats(p[3]); n=int(p[4]); hn=int(p[5]); b=int(p[6]); hexs=p[7]; gc=floats(p[8])
        if w<1 or h<1: continue
        tile=Image.new('RGBA',(n,hn)); pxl=[]
        for i in range(n*hn):
            hx_=hexs[i*8:i*8+8]
            pxl.append(tuple(int(int(hx_[2*k:2*k+2],16)*tint[k]*gc[k]) for k in range(4)))
        tile.putdata(pxl); tile=tile.transpose(Image.FLIP_TOP_BOTTOM)
        sc=min(1.0, w/(2*b), h/(2*b)); bw=max(1,int(round(b*sc))); W2=max(1,int(round(w))); H2=max(1,int(round(h)))
        o9=Image.new('RGBA',(W2,H2),(0,0,0,0))
        xs=[0,b,n-b,n]; ys=[0,b,hn-b,hn]; xd=[0,bw,W2-bw,W2]; yd=[0,bw,H2-bw,H2]
        for j in range(3):
            for i in range(3):
                dw=xd[i+1]-xd[i]; dh=yd[j+1]-yd[j]
                if dw<=0 or dh<=0 or xs[i+1]<=xs[i] or ys[j+1]<=ys[j]: continue
                piece=tile.crop((xs[i],ys[j],xs[i+1],ys[j+1])).resize((dw,dh),Image.BILINEAR)
                o9.paste(piece,(xd[i],yd[j]))
        layer.paste(o9,(int(round(x)),int(round(y))),o9)
    elif kind=='box':
        t=floats(p[3]); mid=floats(p[4]); edge=floats(p[5]); rad=int(p[6]); top=floats(p[7]); bot=floats(p[8])
        mul=lambda c:[c[i]*t[i] for i in range(4)]
        if w<1 or h<1: continue
        r=min(rad-2 if rad>2 else rad, w/2, h/2)
        # vertical gradient fill inside the rounded shape
        mask=Image.new('L',(W,H),0); ImageDraw.Draw(mask).rounded_rectangle([x,y,x+w-1,y+h-1],radius=r,fill=255)
        grad=Image.new('RGBA',(W,H),(0,0,0,0)); gd=ImageDraw.Draw(grad)
        tc=mul(top); bc=mul(bot)
        for yy in range(int(y),int(y+h)):
            f=(yy-y)/max(1,h-1)
            col=[tc[i]+(bc[i]-tc[i])*f for i in range(4)]
            gd.line([(x,yy),(x+w,yy)],fill=c8(*col))
        layer.paste(grad,(0,0),mask)
        ec=mul(edge)
        if ec[3]>0.02: ld.rounded_rectangle([x,y,x+w-1,y+h-1],radius=r,outline=c8(*ec),width=1)
    elif kind=='tris':
        # GL models on top of the menus: blend each triangle over what's there, like the GPU does (drawing
        # into a transparent layer would let each translucent triangle replace the ones under it).
        tv=floats(p[3])
        rgb=img.convert('RGB')
        if os.environ.get('FAST') == '1':
            bd=ImageDraw.Draw(rgb,'RGBA')
            for i in range(0,len(tv),18):
                pts=[(tv[i+k*6], H-tv[i+k*6+1]) for k in range(3)]
                bd.polygon(pts, fill=c8(*tv[i+2:i+6]))
        else:  # like the GPU, over what's drawn so far (gradients, the glow pass, smooth edges)
            from raster import raster
            rgb=Image.fromarray(raster(tv, W, H, int(os.environ.get('SS', '2')), linear=os.environ.get('LINEAR') == '1', base=np.asarray(rgb)))
        img=rgb.convert('RGBA')
        continue
    elif kind=='img':
        n=int(p[3]); hexs=p[4]
        tint=floats(p[5])
        hn=max(1,len(hexs)//8//n)  # not always square: the FadedBar gradient is n x 1
        tile=Image.new('RGBA',(n,hn))
        px=[]
        for i in range(n*hn):
            hx_=hexs[i*8:i*8+8]
            px.append((int(int(hx_[0:2],16)*tint[0]),int(int(hx_[2:4],16)*tint[1]),int(int(hx_[4:6],16)*tint[2]),int(int(hx_[6:8],16)*tint[3])))  # GUI.color multiplies the texture
        tile.putdata(px)
        tile=tile.transpose(Image.FLIP_TOP_BOTTOM).resize((max(1,int(w)),max(1,int(h))),Image.BILINEAR if hn==1 or (len(p)>6 and p[6]=='1') else Image.NEAREST)
        layer.paste(tile,(int(x),int(y)),tile)
    elif kind=='label':
        col=floats(p[3]); size=int(p[4]); align=int(p[5]); bold=p[6]=='1'; text=p[8].replace('\\n','\n')
        f=font(size,bold)
        if p[7]=='1':  # word wrap to the rect width, like Unity
            outl=[]
            for para in text.split('\n'):
                cur=''
                for word in para.split(' '):
                    t2=(cur+' '+word).strip()
                    if ld.textlength(t2,font=f) > w-4 and cur:
                        outl.append(cur); cur=word
                    else: cur=t2
                outl.append(cur)
            text='\n'.join(outl)
        bb=ld.multiline_textbbox((0,0),text,font=f)
        tw,th=bb[2]-bb[0],bb[3]-bb[1]
        hx=[0,1,2,0,1,2][align]; vy=[0,0,0,1,1,1][align]
        tx = x+2 if hx==0 else (x+(w-tw)/2 if hx==1 else x+w-tw-2)
        ty = y+2 if vy==0 else y+(h-th)/2-bb[1]
        ld.multiline_text((tx,ty),text,font=f,fill=c8(*col),align=['left','center','right'][hx])
    # clip (an empty clip area shows nothing; newer Pillow rejects drawing it)
    if cw<1 or ch<1: continue
    m=Image.new('L',(W,H),0); ImageDraw.Draw(m).rectangle([cx,cy,cx+cw-1,cy+ch-1],fill=255)
    clipped=Image.new('RGBA',(W,H),(0,0,0,0)); clipped.paste(layer,(0,0),m)  # mask by the clip only: alpha_composite applies the alpha (once)
    img=Image.alpha_composite(img,clipped)
img.convert('RGB').save(out)
