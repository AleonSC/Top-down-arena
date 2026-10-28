import struct, sys
from PIL import Image, ImageDraw, ImageFont
base=sys.argv[1]; out=sys.argv[2]
W,H=1280,720
img=Image.new('RGB',(W,H),(10,12,18))
d=ImageDraw.Draw(img,'RGBA')
data=open(base+'.tris','rb').read()
v=struct.unpack('<%df'%(len(data)//4),data)
def c8(r,g,b,a): return tuple(int(max(0,min(1,x))*255) for x in (r,g,b,a))
for i in range(0,len(v),18):
    pts=[(v[i+k*6], H-v[i+k*6+1]) for k in range(3)]
    d.polygon(pts, fill=c8(*v[i+2:i+6]))
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
        tv=floats(p[3])
        for i in range(0,len(tv),18):
            pts=[(tv[i+k*6], H-tv[i+k*6+1]) for k in range(3)]
            ld.polygon(pts, fill=c8(*tv[i+2:i+6]))
    elif kind=='img':
        n=int(p[3]); hexs=p[4]
        tint=floats(p[5])
        tile=Image.new('RGBA',(n,n))
        px=[]
        for i in range(n*n):
            hx_=hexs[i*8:i*8+8]
            px.append((int(hx_[0:2],16),int(hx_[2:4],16),int(hx_[4:6],16),int(int(hx_[6:8],16)*tint[3])))
        tile.putdata(px)
        tile=tile.transpose(Image.FLIP_TOP_BOTTOM).resize((max(1,int(w)),max(1,int(h))),Image.NEAREST)
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
    if cw<=0 or ch<=0: continue
    m=Image.new('L',(W,H),0); ImageDraw.Draw(m).rectangle([cx,cy,cx+cw-1,cy+ch-1],fill=255)
    clipped=Image.new('RGBA',(W,H),(0,0,0,0)); clipped.paste(layer,(0,0),Image.composite(layer.getchannel('A'),Image.new('L',(W,H),0),m))
    img=Image.alpha_composite(img,clipped)
img.convert('RGB').save(out)
